import { inspectSpc } from "./audio-formats.js?v=1";
import { readSpcTiming } from "./spc-timing.js?v=1";
import { audioContext, contextForClick, loadSpcEngine, loadSpcAt, stopSpcEngine } from "./spc-engine.js?v=2";
import { claimAudio, releaseAudio, ownsAudio } from "./audio-focus.js?v=1";
import * as library from "./spc-library.js?v=1";

const ownerKey = "snestorage-local-playlist";
const playerUiKey = "snestorage-spc-player-ui";
const maxFileSize = 5 * 1024 * 1024, maxLibrarySize = 100 * 1024 * 1024, maxTracks = 1000;
const views = new Map();
const icons = {
    play: '<path d="m9 5 11 7-11 7V5Z"/>', pause: '<path d="M7 5h3v14H7zM15 5h3v14h-3z"/>',
    previous: '<path d="M5 5v14m14-14L8 12l11 7V5Z"/>', next: '<path d="M19 5v14M5 5l11 7-11 7V5Z"/>',
    stop: '<path d="M6 6h12v12H6Z"/>', shuffle: '<path d="M3 6h3c5 0 7 12 12 12h3m-4-4 4 4-4 4M3 18h3c2 0 4-3 6-6m2-3c1-2 3-3 4-3h3m-4-4 4 4-4 4"/>',
    repeat: '<path d="M4 9V7a3 3 0 0 1 3-3h12m-4-3 4 3-4 3M20 15v2a3 3 0 0 1-3 3H5m4-3-4 3 4 3"/>',
    up: '<path d="m6 15 6-6 6 6"/>', down: '<path d="m6 9 6 6 6-6"/>', close: '<path d="m6 6 12 12M6 18 18 6"/>',
    minimize: '<path d="M5 12h14"/>', restore: '<path d="M8 3H3v5m13-5h5v5M3 16v5h5m13-5v5h-5M8 16V8h8v8H8Z"/>',
    settings: '<path d="M4 7h16M4 17h16M8 4v6m8 4v6"/>'
};
const svg = name => `<svg viewBox="0 0 24 24" aria-hidden="true">${icons[name]}</svg>`;
const button = (command, label, icon) => `<button type="button" data-spc-command="${command}" aria-label="${label}" title="${label}">${svg(icon)}</button>`;
const transport = `
    <div class="spc-current"><span class="spc-album-mark" aria-hidden="true">♪</span><div><span class="spc-eyebrow">NOW PLAYING</span><strong data-spc-title>No track selected</strong><span data-spc-game class="spc-current-game">Add SPC files to build your playlist.</span></div></div>
    <div class="spc-progress"><input data-spc-seek class="spc-seek" type="range" min="0" max="1" step="1" value="0" aria-label="Playback position" disabled><span data-spc-time>0:00 / 0:00</span></div>
    <div class="spc-transport">
        <div class="spc-playback">${button("previous", "Previous track", "previous")}${button("play", "Play", "play")}${button("next", "Next track", "next")}${button("stop", "Stop", "stop")}</div>
        <div class="spc-modes">${button("shuffle", "Shuffle", "shuffle")}${button("repeat", "Repeat: off", "repeat")}<label class="spc-volume">Volume<input data-spc-volume type="range" min="0" max="1" step="0.01" value="0.65" aria-label="Playback volume"></label></div>
    </div>`;

let tracks = [], settings = { volume: .65, shuffle: false, repeat: "off", fallback: 180, useFallback: false, currentId: null };
let ready = false, initializing, importing = false, modifying = false, playing = false, busy = false;
let backend, loadedId, position = 0, playRevision = 0, timer, settingsTimer;
let message = "", storageError = false, expanded = false, minimized = false, dismissed = true, confirmClear = false;
let history = [], shuffleBag = [];
let editingId, completedPlays = 0, fading = false;

// Restore visibility before mounting the dock, so a closed player never flashes open.
// Existing libraries without this preference stay hidden until explicitly opened.
try {
    const saved = JSON.parse(localStorage.getItem(playerUiKey) ?? "null");
    dismissed = saved?.open !== true;
    minimized = saved?.minimized === true;
    expanded = saved?.expanded === true;
} catch { /* Storage may be unavailable; retain the closed default for this visit. */ }

function persistPlayerUi() {
    // Write immediately: closing the tab must not lose a recent close/minimize click.
    try { localStorage.setItem(playerUiKey, JSON.stringify({ open: !dismissed, minimized, expanded })); }
    catch { /* The controls still work for this visit if browser storage is blocked. */ }
}

function currentTrack() { return tracks.find(track => track.id === settings.currentId); }
function duration(track = currentTrack()) {
    if (!track) return 0;
    if (track.timingMode === "continuous") return Infinity;
    if (track.timingMode === "custom" && track.customDuration > 0) return track.customDuration;
    if (track.loopDuration > 0) {
        if (track.plays === 0) return Infinity;
        const loops = track.plays ?? track.tagLoops ?? 1;
        const total = (track.introDuration || 0) + track.loopDuration * loops + (track.endDuration || 0) + (track.fadeDuration || 0);
        return total > 0 ? total : Infinity;
    }
    return track.duration > 0 ? track.duration : settings.useFallback ? settings.fallback : Infinity;
}
function time(seconds) {
    if (!Number.isFinite(seconds)) return "∞";
    const value = Math.max(0, Math.floor(seconds || 0));
    return `${Math.floor(value / 60)}:${String(value % 60).padStart(2, "0")}`;
}
function storageMessage(error) {
    return error?.name === "QuotaExceededError" ? "Your browser has no room for more tracks. Remove some files or free up site storage."
        : error?.message || "Your local library could not be saved. Check your browser's storage settings.";
}

async function ensureReady() {
    if (ready) return true;
    initializing ??= (async () => {
        try {
            const snapshot = await library.readLibrary();
            tracks = snapshot.tracks.filter(track => /^[a-f0-9]{64}$/.test(track.id) && typeof track.title === "string" && typeof track.filename === "string")
                .sort((a, b) => a.order - b.order || a.addedAt - b.addedAt);
            const saved = snapshot.settings;
            settings.volume = Number.isFinite(saved.volume) ? Math.max(0, Math.min(1, saved.volume)) : .65;
            settings.shuffle = saved.shuffle === true;
            settings.repeat = ["off", "all", "one"].includes(saved.repeat) ? saved.repeat : "off";
            settings.fallback = Number.isInteger(saved.fallback) && saved.fallback >= 10 && saved.fallback <= 3600 ? saved.fallback : 180;
            settings.useFallback = saved.useFallback === true;
            settings.currentId = tracks.some(track => track.id === saved.currentId) ? saved.currentId : tracks[0]?.id ?? null;
            ready = true;
            storageError = false;
            message = "";
            resetShuffle();
            return true;
        } catch (error) {
            storageError = true;
            message = storageMessage(error);
            return false;
        } finally { initializing = undefined; render(true); }
    })();
    return initializing;
}

function persistSettings() {
    clearTimeout(settingsTimer);
    settingsTimer = setTimeout(() => {
        library.saveSettings({ ...settings }).catch(error => { message = storageMessage(error); render(); });
    }, 250);
}

export async function mountPage(id) { await mount(id, "page"); }
export async function mountDock(id) { await mount(id, "dock"); }
async function mount(id, type) {
    const root = document.getElementById(id);
    if (!root) return;
    unmount(id);
    const view = { root, type, events: new AbortController() };
    views.set(id, view);
    if (type === "page") {
        // Opening the dedicated tool is an explicit request to show the player again.
        dismissed = false;
        persistPlayerUi();
    }
    if (type === "dock") root.innerHTML = `<div data-spc-dock-body><div class="spc-dock-heading"><a href="/Tools/SPCPlayer">SPC PLAYER</a><div>${button("minimize", "Minimize player", "minimize")}${button("expand", "Show playlist", "up")}${button("close", "Stop and hide player", "close")}</div></div><div data-spc-controls></div><div class="spc-dock-queue" data-spc-dock-queue hidden><ol class="spc-queue" data-spc-queue aria-label="SPC playlist"></ol></div><p data-spc-message class="spc-feedback" role="status"></p></div><div class="spc-mini" data-spc-mini hidden>${button("restore", "Restore player", "restore")}${button("play", "Play", "play")}${button("close", "Stop and hide player", "close")}</div>`;
    root.querySelector("[data-spc-controls]").innerHTML = transport;
    root.addEventListener("click", event => {
        const target = event.target.closest("[data-spc-command]");
        if (target && root.contains(target) && !target.disabled) void command(target.dataset.spcCommand, target.dataset.trackId, view);
    }, { signal: view.events.signal });
    root.addEventListener("input", event => {
        if (event.target.matches("[data-spc-seek]")) {
            view.seeking = true;
            view.seekTrackId = settings.currentId;
            updateProgress();
            return;
        }
        if (!event.target.matches("[data-spc-volume]")) return;
        settings.volume = Number(event.target.value);
        if (loadedId) backend?.setVolume(settings.volume);
        persistSettings();
        render();
    }, { signal: view.events.signal });
    root.addEventListener("change", event => {
        if (event.target.matches("[data-spc-use-fallback]")) {
            settings.useFallback = event.target.checked;
            persistSettings(); render(true);
        }
        if (event.target.matches("[data-spc-timing-mode], [data-spc-custom-length], [data-spc-plays]")) renderEditorHints(view);
        if (event.target.matches("[data-spc-seek]")) {
            const target = Number(event.target.value), id = view.seekTrackId;
            view.seeking = false;
            if (id === settings.currentId) void seekTo(target);
            else updateProgress();
        }
        if (event.target.matches("[data-spc-files]")) {
            const files = Array.from(event.target.files ?? []);
            event.target.value = "";
            void importFiles(files);
        }
        if (event.target.matches("[data-spc-fallback]")) {
            const value = Number(event.target.value);
            if (Number.isInteger(value) && value >= 10 && value <= 3600) {
                settings.fallback = value;
                persistSettings();
                render(true);
            } else { message = "Choose a default track length between 10 and 3,600 seconds."; render(); }
        }
    }, { signal: view.events.signal });
    root.addEventListener("pointercancel", event => {
        if (event.target.matches("[data-spc-seek]")) { view.seeking = false; updateProgress(); }
    }, { signal: view.events.signal });
    root.addEventListener("keydown", event => {
        if (event.key === "Escape" && event.target.matches("[data-spc-seek]")) {
            event.preventDefault(); view.seeking = false; updateProgress();
        }
    }, { signal: view.events.signal });
    if (type === "page") {
        const drop = root.querySelector("[data-spc-drop]");
        for (const name of ["dragenter", "dragover", "dragleave", "drop"]) drop.addEventListener(name, event => {
            event.preventDefault();
            drop.classList.toggle("is-dragging", name === "dragenter" || name === "dragover");
            if (name === "drop") void importFiles(Array.from(event.dataTransfer?.files ?? []));
        }, { signal: view.events.signal });
    }
    render(true);
    await ensureReady();
    if (root.isConnected) render(true);
}

export function unmount(id) {
    const view = views.get(id);
    if (!view) return;
    view.events.abort();
    views.delete(id);
    // Removing a page's controls must never stop playback.
    render();
}

async function command(action, id, view) {
    if (action === "play") { if (playing) pause(); else await play(settings.currentId); }
    else if (action === "track") await play(id);
    else if (action === "stop") { pause(); position = 0; render(); }
    else if (action === "next") await next(false);
    else if (action === "previous") {
        if (elapsed() > 3) await play(settings.currentId, 0);
        else await play(history.pop() ?? tracks[Math.max(0, tracks.findIndex(track => track.id === settings.currentId) - 1)]?.id, 0, false);
    }
    else if (action === "shuffle") { settings.shuffle = !settings.shuffle; resetShuffle(); persistSettings(); render(); }
    else if (action === "repeat") { settings.repeat = { off: "all", all: "one", one: "off" }[settings.repeat]; persistSettings(); render(); }
    else if (action === "repeat-off") {
        settings.repeat = "off"; persistSettings(); render();
        const count = view.root.querySelector("[data-spc-plays]");
        (count?.disabled ? view.root.querySelector("[data-spc-timing-mode]") : count)?.focus();
    }
    else if (action === "expand") { minimized = false; expanded = !expanded; persistPlayerUi(); render(true); }
    else if (action === "minimize") { minimized = true; persistPlayerUi(); render(); view.root.querySelector('[data-spc-command="restore"]')?.focus(); }
    else if (action === "restore") { minimized = false; persistPlayerUi(); render(true); view.root.querySelector('[data-spc-command="minimize"]')?.focus(); }
    else if (action === "close") { pause(); dismissed = true; persistPlayerUi(); render(); }
    else if (action === "remove") await remove(id);
    else if (action === "timing") {
        editingId = id; render(); view.root.querySelector("[data-spc-timing-mode]")?.focus();
    }
    else if (action === "use-custom") {
        view.root.querySelector("[data-spc-timing-mode]").value = "custom";
        renderEditorHints(view);
        view.root.querySelector("[data-spc-custom-length]")?.focus();
    }
    else if (action === "save-timing") await saveTiming(view);
    else if (action === "cancel-timing") {
        const trackId = editingId;
        editingId = null; render();
        view.root.querySelector(`[data-spc-command="timing"][data-track-id="${trackId}"]`)?.focus();
    }
    else if (action === "up" || action === "down") {
        await move(id, action === "up" ? -1 : 1);
        view.root.querySelector(`[data-spc-command="${action}"][data-track-id="${id}"]`)?.focus();
    }
    else if (action === "clear") { confirmClear = true; render(); }
    else if (action === "cancel-clear") { confirmClear = false; render(); }
    else if (action === "confirm-clear") await clear();
    else if (action === "retry") await ensureReady();
}

async function importFiles(files) {
    if (!files.length || importing || modifying) return;
    if (!await ensureReady() || importing || modifying) return;
    importing = true;
    confirmClear = false;
    let added = 0, duplicates = 0, total = tracks.reduce((sum, track) => sum + track.size, 0);
    const errors = [];
    const chosen = files.slice(0, maxTracks);
    if (files.length > chosen.length) errors.push(`Only the first ${maxTracks} files were considered.`);
    try {
        for (let index = 0; index < chosen.length; index++) {
            const file = chosen[index];
            message = `Adding SPC files… ${index + 1} / ${chosen.length}`;
            render();
            try {
                if (!/\.spc$/i.test(file.name)) throw new Error("Only .spc files are supported.");
                if (!file.size || file.size > maxFileSize) throw new Error("Each SPC must be nonempty and no larger than 5 MB.");
                const bytes = new Uint8Array(await file.arrayBuffer());
                const tags = { ...inspectSpc(bytes), ...readSpcTiming(bytes) };
                const hash = new Uint8Array(await crypto.subtle.digest("SHA-256", bytes));
                const id = Array.from(hash, value => value.toString(16).padStart(2, "0")).join("");
                if (tracks.some(track => track.id === id)) { duplicates++; continue; }
                if (tracks.length >= maxTracks || total + file.size > maxLibrarySize)
                    throw new Error("The local library limit is 1,000 tracks or 100 MB.");
                const track = { ...tags, id, filename: file.name, title: tags.title || file.name.replace(/\.spc$/i, ""),
                    size: file.size, order: (tracks.at(-1)?.order ?? -1) + 1, addedAt: Date.now() };
                await library.addTrack(track, file);
                tracks.push(track);
                total += file.size;
                added++;
            } catch (error) {
                if (errors.length < 4) errors.push(`${file.name}: ${storageMessage(error)}`);
                if (["QuotaExceededError", "InvalidStateError", "UnknownError"].includes(error.name)) break;
            }
        }
        settings.currentId ??= tracks[0]?.id ?? null;
        resetShuffle();
        persistSettings();
        message = `${added} ${added === 1 ? "track" : "tracks"} added.${duplicates ? ` ${duplicates} already in your library.` : ""}${errors.length ? ` ${errors.join(" ")}` : " Saved only in this browser."}`;
    } finally { importing = false; render(true); }
}

async function play(id, from, remember = true, preservePlayCount = false) {
    const track = tracks.find(item => item.id === id);
    if (!track || busy) return;
    const resumeAt = from ?? (id === settings.currentId ? position : 0);
    const revision = ++playRevision;
    try {
        const context = contextForClick();
        if (loadedId) stopSpcEngine(backend);
        loadedId = null;
        clearInterval(timer);
        claimAudio(ownerKey, () => {
            pause(false);
            message = "Paused while another sound preview plays.";
            render();
        });
        if (remember && settings.currentId && settings.currentId !== id) {
            history.push(settings.currentId);
            if (history.length > maxTracks) history.shift();
        }
        settings.currentId = id;
        if (!preservePlayCount) completedPlays = 0;
        fading = false;
        position = Math.floor(Math.min(Math.max(0, resumeAt), Math.max(0, duration(track) - 1)));
        for (const view of views.values()) view.seeking = false;
        shuffleBag = shuffleBag.filter(candidate => candidate !== id);
        dismissed = false;
        persistPlayerUi();
        playing = false;
        busy = true;
        message = position > 0 ? `Seeking to ${time(position)}…` : "Loading track…";
        render();
        const file = await library.readTrackFile(id);
        if (revision !== playRevision) return;
        if (!(file instanceof Blob)) throw new Error("This track is missing from browser storage. Remove it and add the SPC again.");
        if (!file.size || file.size > maxFileSize) throw new Error("This stored SPC is empty or exceeds the 5 MB limit. Remove it and add a valid file.");
        const bytes = new Uint8Array(await file.arrayBuffer());
        if (revision !== playRevision) return;
        inspectSpc(bytes);
        let timingWarning = "";
        if (track.timingVersion !== 2) {
            Object.assign(track, readSpcTiming(bytes));
            try { await library.saveTrackOrder([track]); }
            catch { timingWarning = "Updated timing is available for this session but could not be saved in the browser."; }
            if (revision !== playRevision) return;
            render(true);
        }
        const engine = await loadSpcEngine(context);
        if (revision !== playRevision) return;
        backend = engine;
        loadedId = id;
        position = Math.floor(Math.min(Math.max(0, resumeAt), Math.max(0, duration(track) - 1)));
        if (!await loadSpcAt(backend, bytes, position, () => revision === playRevision)) return;
        backend.setVolume(settings.volume);
        await context.resume();
        if (revision !== playRevision) return;
        if (context.state !== "running") throw new Error("Press Play again to allow sound in this browser.");
        playing = true;
        message = timingWarning;
        persistSettings();
        timer = setInterval(tick, 250);
    } catch (error) {
        if (revision !== playRevision) return;
        pause();
        message = error.message || "This SPC could not be played.";
        render();
    } finally {
        if (revision === playRevision) { busy = false; render(); }
    }
}

function elapsed() { return loadedId && playing ? Math.max(0, backend.getTime()) : position; }
async function seekTo(value) {
    if (busy || !currentTrack() || !Number.isFinite(value)) { updateProgress(); return; }
    const target = Math.floor(Math.max(0, Math.min(value, duration() - 1)));
    if (playing) await play(settings.currentId, target, false, true);
    else {
        position = target;
        message = `Playback position set to ${time(target)}.`;
        render();
    }
}
function pause(suspend = true) {
    const owned = ownsAudio(ownerKey);
    position = elapsed();
    playRevision++;
    clearInterval(timer);
    for (const view of views.values()) view.seeking = false;
    if (loadedId) stopSpcEngine(backend);
    loadedId = null;
    busy = playing = false;
    fading = false;
    releaseAudio(ownerKey);
    if (suspend && owned) audioContext?.suspend().catch(() => {});
    render();
}
function tick() {
    if (!playing || busy) return;
    if (elapsed() >= duration() && ![...views.values()].some(view => view.seeking)) { void next(true); return; }
    const remaining = duration() - elapsed();
    const track = currentTrack(), fade = track?.timingMode === "custom" ? 1 : track?.fadeDuration || 0;
    if (fade > 0 && remaining < fade) { backend.setVolume(settings.volume * Math.max(0, remaining / fade)); fading = true; }
    else if (fading) { backend.setVolume(settings.volume); fading = false; }
    updateProgress();
}

function resetShuffle() {
    shuffleBag = tracks.map(track => track.id).filter(id => id !== settings.currentId);
    for (let index = shuffleBag.length - 1; index > 0; index--) {
        const other = Math.floor(Math.random() * (index + 1));
        [shuffleBag[index], shuffleBag[other]] = [shuffleBag[other], shuffleBag[index]];
    }
}
async function next(automatic) {
    if (busy || !tracks.length) return;
    if (automatic && settings.repeat === "one") { await play(settings.currentId, 0); return; }
    const track = currentTrack();
    // xid6 loop timing extends native playback; without it, counted plays
    // restart the full snapshot after its chosen duration.
    if (automatic && !(track?.loopDuration > 0 && track.timingMode !== "custom")) {
        const plays = track?.plays ?? 1;
        if (plays === 0 || completedPlays + 1 < plays) {
            completedPlays++;
            await play(settings.currentId, 0, false, true);
            return;
        }
    }
    let id;
    if (settings.shuffle) {
        if (!shuffleBag.length && settings.repeat === "all") resetShuffle();
        id = shuffleBag.shift();
        if (!id && settings.repeat === "all") id = settings.currentId;
    } else {
        const index = tracks.findIndex(track => track.id === settings.currentId);
        id = tracks[index + 1]?.id;
        if (!id && settings.repeat === "all") id = tracks[0]?.id;
    }
    if (id) await play(id, 0);
    else { pause(); position = 0; message = "End of playlist."; render(); }
}

async function remove(id) {
    if (importing || modifying) return;
    modifying = true;
    render();
    try {
        await library.deleteTrack(id);
        if (settings.currentId === id) { pause(); position = 0; settings.currentId = null; }
        tracks = tracks.filter(track => track.id !== id);
        if (editingId === id) editingId = null;
        settings.currentId ??= tracks[0]?.id ?? null;
        history = history.filter(item => item !== id);
        resetShuffle();
        persistSettings();
        message = "Track removed from this browser.";
    } catch (error) { message = storageMessage(error); }
    finally { modifying = false; render(true); }
}
async function move(id, direction) {
    if (importing || modifying) return;
    const index = tracks.findIndex(track => track.id === id), other = index + direction;
    if (index < 0 || other < 0 || other >= tracks.length) return;
    modifying = true;
    render();
    try {
        const reordered = tracks.map(track => ({ ...track }));
        [reordered[index], reordered[other]] = [reordered[other], reordered[index]];
        reordered.forEach((track, order) => { track.order = order; });
        await library.saveTrackOrder(reordered);
        tracks = reordered;
    } catch (error) { message = storageMessage(error); }
    finally { modifying = false; render(true); }
}

function renderEditorHints(view) {
    if (view.type !== "page") return;
    const track = tracks.find(item => item.id === editingId);
    if (!track) return;
    const find = name => view.root.querySelector(`[data-spc-${name}]`);
    const mode = find("timing-mode").value;
    const loopMode = mode === "auto" && track.loopDuration > 0;
    const hasLength = mode === "custom" || Number.isFinite(duration({ ...track, timingMode: "auto", plays: null }));
    find("custom-length").disabled = modifying || busy || mode !== "custom";
    find("plays").disabled = modifying || busy || mode === "continuous" || !hasLength;
    find("plays-label").textContent = loopMode ? "2. How many loops?" : "2. How many times?";
    find("plays").querySelector('[value="tag"]').textContent = loopMode
        ? `File default (${track.tagLoops ?? 1} ${(track.tagLoops ?? 1) === 1 ? "loop" : "loops"})` : "1 time (default)";
    for (const option of find("plays").options) {
        if (Number(option.value) > 0) option.textContent = loopMode
            ? `${option.value} ${option.value === "1" ? "loop" : "loops"}` : `${option.value} ${option.value === "1" ? "time" : "times"}`;
    }
    find("enable-count").hidden = hasLength || mode === "continuous";
    find("enable-count").disabled = modifying || busy;
    find("repeat-warning").hidden = settings.repeat !== "one";
    find("timing-hint").textContent = mode === "continuous" ? "This track keeps playing until you choose Next or Stop."
        : !hasLength ? "This file has no usable timing. Set a length first so the player knows when one play ends."
        : loopMode ? "The intro plays once, followed by the chosen number of loops. It then fades out and moves to the next song."
        : "After the chosen length, another play starts from the beginning. Once the total count is reached, the next song starts.";
    const selected = find("plays").value;
    const count = selected === "tag" ? (loopMode ? track.tagLoops ?? 1 : 1) : Number(selected);
    const seconds = Number(find("custom-length").value);
    if (mode === "continuous" || !hasLength) find("play-plan").textContent = "Keeps playing until you press Next or Stop.";
    else if (!count && selected !== "tag") find("play-plan").textContent = "Keeps repeating this song until you press Next or Stop.";
    else if (mode === "custom" && (!Number.isInteger(seconds) || seconds < 1 || seconds > 21600))
        find("play-plan").textContent = "Enter a length between 1 and 21,600 seconds.";
    else {
        const length = duration({ ...track, timingMode: mode, customDuration: seconds, plays: count });
        find("play-plan").textContent = loopMode
            ? `Intro → ${count} ${count === 1 ? "loop" : "loops"} → next song. About ${time(length)} in total.`
            : `Play ${count === 1 ? "once" : `${count} times`} (${time(length)} each) → next song. About ${time(length * count)} in total.`;
    }
}

async function saveTiming(view) {
    if (modifying || importing || busy) return;
    const track = tracks.find(item => item.id === editingId);
    if (!track) return;
    const find = name => view.root.querySelector(`[data-spc-${name}]`);
    const mode = find("timing-mode").value, seconds = Number(find("custom-length").value);
    const plays = find("plays").value === "tag" ? null : Number(find("plays").value);
    if (!["auto", "custom", "continuous"].includes(mode) || mode === "custom" &&
        (!Number.isInteger(seconds) || seconds < 1 || seconds > 21600) || plays !== null && ![0, 1, 2, 3, 4, 5, 10].includes(plays)) {
        message = "Choose a length between 1 and 21,600 seconds and a valid play count."; render(); return;
    }
    modifying = true;
    render();
    try {
        const updated = { ...track, timingMode: mode, customDuration: seconds, plays: mode === "continuous" ? null : plays };
        await library.saveTrackOrder([updated]);
        Object.assign(track, updated);
        if (track.id === settings.currentId) {
            completedPlays = 0; fading = false;
            if (loadedId) backend.setVolume(settings.volume);
        }
        editingId = null;
        message = "Track playback settings saved in this browser.";
    } catch (error) { message = storageMessage(error); }
    finally {
        modifying = false; render(true);
        if (!editingId) view.root.querySelector(`[data-spc-command="timing"][data-track-id="${track.id}"]`)?.focus();
    }
}

async function clear() {
    if (importing || modifying) return;
    modifying = true;
    render();
    try {
        await library.clearTracks();
        pause();
        position = 0;
        tracks = []; history = []; shuffleBag = [];
        editingId = null;
        settings.currentId = null;
        persistSettings();
        message = "Your local playlist has been cleared.";
        confirmClear = false;
    } catch (error) { message = storageMessage(error); }
    finally { modifying = false; render(true); }
}

function render(rebuildQueue = false) {
    const track = currentTrack(), pageVisible = [...views.values()].some(view => view.type === "page" && view.root.isConnected);
    for (const view of views.values()) {
        const root = view.root, find = name => root.querySelector(`[data-spc-${name}]`);
        if (!root.isConnected) continue;
        if (view.type === "dock") {
            root.hidden = pageVisible || dismissed || !track;
            root.classList.toggle("is-expanded", expanded && !minimized);
            root.classList.toggle("is-minimized", minimized);
            find("dock-body").hidden = minimized;
            find("mini").hidden = !minimized;
            find("dock-queue").hidden = !expanded || minimized;
            const restore = root.querySelector('[data-spc-command="restore"]');
            restore.title = `Restore player: ${track?.title ?? "SPC Player"}`;
            restore.setAttribute("aria-label", restore.title);
            const expand = root.querySelector('[data-spc-command="expand"]');
            expand.setAttribute("aria-expanded", String(expanded));
            expand.setAttribute("aria-label", expanded ? "Hide playlist" : "Show playlist");
        }
        root.classList.toggle("is-playing", playing);
        find("title").textContent = track?.title ?? "Your SNES soundtrack starts here";
        find("game").textContent = track?.game || track?.filename || "Add SPC files to build your playlist.";
        for (const playButton of root.querySelectorAll('[data-spc-command="play"]')) {
            playButton.innerHTML = svg(playing ? "pause" : "play");
            playButton.setAttribute("aria-label", busy ? "Loading track" : playing ? "Pause" : "Play");
            playButton.title = busy ? "Loading track…" : playing ? "Pause" : "Play";
        }
        find("seek").disabled = !track || busy;
        for (const element of root.querySelectorAll('[data-spc-command="play"], [data-spc-command="previous"], [data-spc-command="next"]')) element.disabled = !track || busy;
        root.querySelector('[data-spc-command="stop"]').disabled = !track;
        for (const element of root.querySelectorAll('[data-spc-command="shuffle"], [data-spc-command="repeat"]')) {
            const shuffle = element.dataset.spcCommand === "shuffle", active = shuffle ? settings.shuffle : settings.repeat !== "off";
            element.classList.toggle("is-active", active);
            element.setAttribute("aria-pressed", String(active));
            const label = shuffle ? `Shuffle: ${settings.shuffle ? "on" : "off"}` : `Repeat: ${settings.repeat}`;
            element.setAttribute("aria-label", label); element.title = label;
            element.dataset.repeat = shuffle ? "" : settings.repeat;
        }
        if (document.activeElement !== find("volume")) find("volume").value = String(settings.volume);
        find("message").textContent = message;
        if (view.type === "page") {
            find("files").disabled = !ready || importing || modifying;
            find("drop").classList.toggle("is-disabled", !ready || importing || modifying);
            find("summary").textContent = ready ? `${tracks.length} ${tracks.length === 1 ? "track" : "tracks"} · ${(tracks.reduce((sum, item) => sum + item.size, 0) / 1024 / 1024).toFixed(1)} MB stored in this browser` : storageError ? "Local library unavailable" : "Opening your local library…";
            find("empty").hidden = tracks.length > 0 || !ready;
            find("retry").hidden = !storageError;
            find("confirm").hidden = !confirmClear;
            root.querySelector('[data-spc-command="clear"]').disabled = !tracks.length || importing || modifying;
            for (const element of find("confirm").querySelectorAll("button")) element.disabled = modifying;
            if (document.activeElement !== find("fallback")) find("fallback").value = String(settings.fallback);
            find("use-fallback").checked = settings.useFallback;
            find("fallback").disabled = !settings.useFallback;
            const edited = tracks.find(item => item.id === editingId);
            find("track-settings").hidden = !edited;
            if (edited) {
                find("edit-title").textContent = edited.title;
                find("edit-info").textContent = edited.loopDuration > 0
                    ? `SPC timing: intro ${time(edited.introDuration)} · loop ${time(edited.loopDuration)} · fade ${time(edited.fadeDuration)}.`
                    : edited.duration ? `Tagged playback length: ${time(edited.duration)}. No loop boundaries are available.`
                    : "This SPC does not provide a playback length or loop boundaries.";
                if (view.editorLoadedId !== editingId) {
                    view.editorLoadedId = editingId;
                    find("timing-mode").value = edited.timingMode || "auto";
                    find("custom-length").value = String(edited.customDuration || Math.ceil(edited.duration || settings.fallback));
                    find("plays").value = edited.plays == null ? "tag" : String(edited.plays);
                }
                find("timing-mode").disabled = modifying || busy;
                for (const element of find("track-settings").querySelectorAll("button")) element.disabled = modifying || busy;
                renderEditorHints(view);
            } else view.editorLoadedId = null;
        }
        if (rebuildQueue) view.queueDirty = true;
        if (view.type === "dock" && (!expanded || minimized || root.hidden) && find("queue").childElementCount) {
            find("queue").replaceChildren();
            view.queueDirty = true;
        }
        if (view.queueDirty && (view.type === "page" || expanded && !minimized && !root.hidden)) {
            renderQueue(view);
            view.queueDirty = false;
            view.queueState = null;
        }
        const queueState = `${settings.currentId}:${busy}:${importing}:${modifying}`;
        if (view.queueState !== queueState) {
            view.queueState = queueState;
            for (const row of root.querySelectorAll("[data-spc-row]")) {
                const active = row.dataset.spcRow === settings.currentId;
                row.classList.toggle("is-current", active);
                if (active) row.setAttribute("aria-current", "true"); else row.removeAttribute("aria-current");
                for (const element of row.querySelectorAll("button")) element.disabled = importing || modifying || busy ||
                    element.dataset.spcCommand === "up" && row === row.parentElement.firstElementChild ||
                    element.dataset.spcCommand === "down" && row === row.parentElement.lastElementChild;
            }
        }
    }
    const dockVisible = [...views.values()].some(view => view.type === "dock" && view.root.isConnected && !view.root.hidden);
    document.documentElement.classList.toggle("has-spc-dock", dockVisible);
    document.documentElement.classList.toggle("has-spc-mini", dockVisible && minimized);
    updateProgress();
}

function renderQueue(view) {
    const fragment = document.createDocumentFragment();
    tracks.forEach((track, index) => {
        const row = document.createElement("li"); row.dataset.spcRow = track.id;
        const play = document.createElement("button"); play.type = "button"; play.className = "spc-track-select";
        play.dataset.spcCommand = "track"; play.dataset.trackId = track.id;
        play.setAttribute("aria-label", `Play ${track.title}`);
        const number = document.createElement("span"); number.className = "spc-track-number"; number.textContent = String(index + 1).padStart(2, "0");
        const copy = document.createElement("span"); copy.className = "spc-track-copy";
        const title = document.createElement("strong"); title.textContent = track.title;
        const game = document.createElement("small"); game.textContent = track.game || track.filename;
        copy.append(title, game);
        const length = document.createElement("span"); length.className = "spc-track-length";
        length.textContent = time(duration(track));
        if (Number.isFinite(duration(track)) && track.plays != null && !(track.loopDuration > 0 && track.timingMode !== "custom"))
            length.textContent += track.plays ? ` · ${track.plays} ${track.plays === 1 ? "play" : "plays"}` : " · repeat";
        length.title = !Number.isFinite(duration(track)) ? "Plays until you skip this track" : track.timingMode === "custom" ? "Custom playback length" : "Length from timing tags or your chosen default";
        play.append(number, copy, length); row.append(play);
        const actions = document.createElement("div"); actions.className = "spc-track-actions";
        for (const [command, label, icon] of view.type === "page" ? [["timing", "Track playback settings", "settings"], ["up", "Move up", "up"], ["down", "Move down", "down"], ["remove", "Remove track", "close"]] : [["remove", "Remove track", "close"]]) {
            const element = document.createElement("button"); element.type = "button"; element.dataset.spcCommand = command; element.dataset.trackId = track.id;
            element.setAttribute("aria-label", `${label}: ${track.title}`); element.title = label; element.innerHTML = svg(icon); actions.append(element);
            if (command === "timing") {
                const caption = document.createElement("span"); caption.textContent = "Playback"; element.append(caption);
            }
        }
        row.append(actions); fragment.append(row);
    });
    view.root.querySelector("[data-spc-queue]").replaceChildren(fragment);
}

function updateProgress() {
    const value = elapsed(), length = currentTrack() ? duration() : 0;
    for (const view of views.values()) {
        const seek = view.root.querySelector("[data-spc-seek]");
        const finite = Number.isFinite(length);
        if (!view.seeking) {
            seek.max = finite ? length || 1 : Math.ceil(Math.max(value + 60, settings.fallback) / 60) * 60;
            seek.value = String(Math.min(value, Number(seek.max)));
        }
        const displayed = view.seeking ? Number(seek.value) : value;
        seek.style.setProperty("--spc-position", `${Number(seek.max) ? Math.min(100, Math.max(0, displayed / Number(seek.max) * 100)) : 0}%`);
        seek.setAttribute("aria-valuetext", finite ? `${time(displayed)} of ${time(length)}` : `${time(displayed)}, no preset ending`);
        view.root.querySelector("[data-spc-time]").textContent = `${time(displayed)} / ${finite ? time(length) : "∞"}`;
    }
}

new MutationObserver(() => {
    let changed = false;
    for (const [id, view] of views) if (!view.root.isConnected) { view.events.abort(); views.delete(id); changed = true; }
    if (changed) render();
}).observe(document.documentElement, { childList: true, subtree: true });
window.addEventListener("pagehide", () => { pause(); });
