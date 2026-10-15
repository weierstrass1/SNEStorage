import { inspectSpc } from "./audio-formats.js?v=1";
import { readPreviewBlob } from "./resource-media.js?v=4";

const players = new Map();
let activePlayer, audioContext, spcPromise, spcBackend;
const playIcon = '<svg viewBox="0 0 24 24" aria-hidden="true"><path d="M8 5v14l11-7Z"/></svg>';
const pauseIcon = '<svg viewBox="0 0 24 24" aria-hidden="true"><path d="M6 5h4v14H6Zm8 0h4v14h-4Z"/></svg>';

function contextForClick() {
    const Context = window.AudioContext || window.webkitAudioContext;
    if (!Context) throw new Error("Your browser does not support this sound preview.");
    audioContext ??= new Context();
    // Run synchronously from the native click handler, before loading files or WASM.
    audioContext.resume().catch(() => {});
    return audioContext;
}

function loadSpcEngine(context) {
    if (spcBackend?.status === 1) return Promise.resolve(spcBackend);
    if (spcBackend) return Promise.reject(new Error("The SPC engine stopped after an invalid snapshot. Reload the page to restart it."));
    if (spcPromise) return spcPromise;
    spcPromise = new Promise((resolve, reject) => {
        const script = document.createElement("script");
        const assetUrl = new URL("../lib/spc-player/spc-core.js?v=1", import.meta.url);
        const timer = setTimeout(() => reject(new Error("The SPC player could not finish loading. Reload the page and try again.")), 15000);
        window.SNEStorageSPCModule = {
            audioContext: context,
            locateFile: name => new URL(name, assetUrl).href,
            onAbort: () => { clearTimeout(timer); reject(new Error("The SPC player could not start.")); },
            onRuntimeInitialized: () => queueMicrotask(() => {
                clearTimeout(timer);
                spcBackend = window.SMWCentral?.SPCPlayer?.Backend;
                if (spcBackend?.status !== 1) reject(new Error("The SPC player is unavailable in this browser."));
                else resolve(spcBackend);
                delete window.SNEStorageSPCModule;
            })
        };
        script.src = assetUrl.href;
        script.onerror = () => { clearTimeout(timer); reject(new Error("The SPC player could not be downloaded. Reload the page and try again.")); };
        document.head.append(script);
    });
    return spcPromise;
}

const template = `
    <header class="sound-heading"><span class="sound-mark" aria-hidden="true">♪</span><div><span class="sound-format"></span><strong class="sound-name"></strong></div></header>
    <div class="sound-wave" aria-hidden="true"><i></i><i></i><i></i><i></i><i></i><i></i><i></i><i></i><i></i></div>
    <div class="sound-timeline"><input class="sound-seek" type="range" min="0" max="1" step=".01" value="0" aria-label="Playback position" disabled><span class="sound-time">0:00</span></div>
    <div class="sound-buttons"><button type="button" class="sound-play" aria-label="Play sound preview">${playIcon}<span>Play</span></button>
    <button type="button" class="sound-restart" aria-label="Restart sound preview"><svg viewBox="0 0 24 24" aria-hidden="true"><path d="M4 9a8 8 0 1 1 0 6M4 3v6h6"/></svg></button>
    <button type="button" class="sound-stop" aria-label="Stop sound preview"><svg viewBox="0 0 24 24" aria-hidden="true"><path d="M6 6h12v12H6Z"/></svg></button>
    <label class="sound-loop-label"><input class="sound-loop" type="checkbox">Loop</label></div>
    <label class="sound-volume-label">Volume<input class="sound-volume" type="range" min="0" max="1" step=".01" value=".65" aria-label="Preview volume"></label>
    <label class="sound-rate-label" hidden>Sample rate<select class="sound-rate" aria-label="BRR sample playback rate"><option value="8000">8 kHz</option><option value="11025">11.025 kHz</option><option value="16000">16 kHz</option><option value="22050">22.05 kHz</option><option value="32000" selected>32 kHz</option></select></label>
    <p class="sound-feedback" role="status" aria-live="polite"></p>`;

export function initializeAudioPreview(id, source, kind, filename) {
    const root = document.getElementById(id);
    if (!root) return;
    disposeAudioPreview(id);
    root.innerHTML = template; // Only constant markup; file names/SPC tags use textContent below.
    const find = name => root.querySelector(`.sound-${name}`);
    const state = {
        id, root, source, kind, controller: new AbortController(), listeners: new AbortController(),
        ready: false, busy: false, disposed: false, playing: false, position: 0, duration: 0,
        volume: .65, rate: 32000, loop: false, timer: null, native: null, blobUrl: null,
        bytes: null, samples: null, buffer: null, node: null, gain: null, spcLoaded: false,
        play: find("play"), seek: find("seek"), time: find("time"), feedback: find("feedback")
    };
    players.set(id, state);
    find("name").textContent = filename || "Sound preview";
    find("format").textContent = kind === "spc" ? "SNES MUSIC · SPC" : kind === "brr" ? "SNES SAMPLE · BRR" : "AUDIO PREVIEW";
    find("rate-label").hidden = kind !== "brr";
    if (kind === "brr") state.feedback.textContent = "Change the sample rate to adjust pitch. BRR files do not contain instrument envelopes.";
    const on = (element, event, handler) => element.addEventListener(event, handler, { signal: state.listeners.signal });
    on(state.play, "click", () => state.playing ? pause(state) : play(state));
    on(find("restart"), "click", () => { stop(state, false); play(state); });
    on(find("stop"), "click", () => stop(state));
    on(find("volume"), "input", event => {
        state.volume = Number(event.target.value);
        if (state.native) state.native.volume = state.volume;
        if (state.gain) state.gain.gain.value = state.volume;
        if (state.spcLoaded) spcBackend?.setVolume(state.volume);
    });
    on(find("loop"), "change", event => {
        state.loop = event.target.checked;
        if (state.native) state.native.loop = state.loop;
        if (state.node) state.node.loop = state.loop;
    });
    on(find("rate"), "change", event => {
        const playing = state.playing;
        stop(state, false);
        state.rate = Number(event.target.value);
        state.buffer = null;
        if (state.samples) state.duration = state.samples.samples.length / state.rate;
        update(state);
        if (playing) play(state);
    });
    on(state.seek, "input", event => {
        const position = Number(event.target.value);
        if (state.native) state.native.currentTime = position;
        else if (state.kind === "brr") {
            const playing = state.playing;
            pause(state);
            state.position = position;
            if (playing) play(state);
        }
        update(state);
    });
}

function activate(state) {
    if (activePlayer && activePlayer !== state) {
        const previous = activePlayer;
        stop(previous, false);
        // Keep only the active BRR's expanded PCM data, rather than 20 decoded samples.
        if (previous.kind === "brr") {
            previous.samples = previous.buffer = null;
            previous.ready = false;
        }
    }
    for (const media of document.querySelectorAll("audio, video")) if (media !== state.native) media.pause();
    activePlayer = state;
}

async function prepare(state, context, signal) {
    const source = new URL(state.source, document.baseURI);
    if (source.origin !== window.location.origin || !["https:", "http:", "blob:"].includes(source.protocol))
        throw new Error("This preview address is unavailable.");
    const response = await fetch(source, { credentials: "same-origin", signal });
    if (!response.ok) {
        if (response.status === 401) window.dispatchEvent(new Event("snestorage-session-rejected"));
        throw new Error(response.status === 401 || response.status === 403 ? "You do not have access to this sound preview."
            : response.status === 404 ? "This sound preview could not be found." : "This sound preview could not be loaded. Try again later.");
    }
    const blob = await readPreviewBlob(response, response.headers.get("Content-Type") ?? "", signal);
    if (!blob) throw new Error("Sound previews must be nonempty and no larger than 5 MB.");
    signal.throwIfAborted();
    if (state.kind === "spc") {
        const bytes = new Uint8Array(await blob.arrayBuffer());
        signal.throwIfAborted();
        state.bytes = bytes;
        const tags = inspectSpc(state.bytes);
        state.duration = tags.duration ?? 0;
        if (tags.title) state.root.querySelector(".sound-name").textContent = tags.title;
        state.feedback.textContent = tags.game || (tags.duration ? "" : "No track duration was provided. Use Stop when you finish listening.");
        await loadSpcEngine(context);
    } else if (state.kind === "brr") {
        const { decodeBrr } = await import("./brr-decoder.js?v=1");
        signal.throwIfAborted();
        const samples = await decodeBrr(new Uint8Array(await blob.arrayBuffer()), signal);
        signal.throwIfAborted();
        state.samples = samples;
        state.duration = state.samples.samples.length / state.rate;
    } else {
        state.blobUrl = URL.createObjectURL(blob);
        state.native = document.createElement("audio");
        state.native.preload = "metadata";
        state.native.hidden = true;
        state.native.volume = state.volume;
        state.native.loop = state.loop;
        state.native.addEventListener("loadedmetadata", () => {
            state.duration = Number.isFinite(state.native.duration) ? state.native.duration : 0;
            update(state);
        }, { signal: state.listeners.signal });
        state.native.addEventListener("ended", () => { state.playing = false; state.position = 0; clearInterval(state.timer); update(state); }, { signal: state.listeners.signal });
        state.native.addEventListener("error", () => {
            stop(state);
            state.feedback.textContent = "This browser could not play the audio format. Try a browser that supports this format.";
        }, { signal: state.listeners.signal });
        state.root.append(state.native);
        state.native.src = state.blobUrl;
    }
    signal.throwIfAborted();
    state.ready = true;
}

async function play(state) {
    if (state.busy || state.disposed) return;
    state.busy = true;
    state.play.disabled = true;
    const token = state.controller.signal;
    try {
        const context = state.kind === "audio" ? null : contextForClick();
        activate(state);
        if (!state.ready) {
            state.feedback.textContent = "Loading sound preview…";
            await prepare(state, context, token);
        }
        token.throwIfAborted();
        if (state.disposed || activePlayer !== state) return;
        if (state.kind === "spc") {
            if (!state.spcLoaded) { state.spcLoaded = true; spcBackend.loadSPC(state.bytes); }
            spcBackend.setVolume(state.volume);
            await context.resume();
        } else if (state.kind === "brr") {
            await context.resume();
            token.throwIfAborted();
            if (state.disposed || activePlayer !== state) return;
            if (!state.buffer) {
                state.buffer = context.createBuffer(1, state.samples.samples.length, state.rate);
                state.buffer.copyToChannel(state.samples.samples, 0);
            }
            state.gain ??= context.createGain();
            state.gain.gain.value = state.volume;
            state.gain.disconnect();
            state.gain.connect(context.destination);
            const node = context.createBufferSource();
            node.buffer = state.buffer;
            node.loop = state.loop;
            node.loopStart = state.samples.loopSample / state.rate;
            node.loopEnd = state.duration;
            node.connect(state.gain);
            state.node = node;
            state.startedAt = context.currentTime - state.position;
            node.onended = () => { node.disconnect(); if (state.node === node) { state.node = null; state.playing = false; state.position = 0; clearInterval(state.timer); update(state); } };
            node.start(0, Math.min(state.position, Math.max(0, state.duration - .0001)));
        } else await state.native.play();
        token.throwIfAborted();
        state.playing = true;
        if (state.kind !== "brr" && state.duration) state.feedback.textContent = "";
        if (state.kind === "brr") state.feedback.textContent = "Sample playback · adjust the sample rate to change pitch.";
        clearInterval(state.timer);
        state.timer = setInterval(() => {
            if (state.kind === "spc" && !state.loop && state.duration && elapsed(state) >= state.duration) stop(state);
            update(state);
        }, 250);
        update(state);
    } catch (error) {
        if (!state.disposed && error.name !== "AbortError") {
            stop(state);
            state.feedback.textContent = state.kind === "spc" && spcBackend?.status !== 1 && spcBackend
                ? "This SPC snapshot could not be played. Reload the page to restart the SPC engine."
                : error.name === "NotAllowedError" ? "Press Play again to allow sound in this browser."
                : error.message || "This sound preview could not be played.";
        }
    } finally {
        state.busy = false;
        if (!state.disposed) state.play.disabled = false;
    }
}

function elapsed(state) {
    if (state.native) return state.native.currentTime;
    if (state.spcLoaded) return spcBackend?.getTime() ?? 0;
    const position = state.playing && state.kind === "brr" ? Math.max(0, audioContext.currentTime - state.startedAt) : state.position;
    if (state.loop && state.kind === "brr" && state.duration && position >= state.duration) {
        const loopStart = state.samples.loopSample / state.rate;
        return loopStart + (position - loopStart) % (state.duration - loopStart);
    }
    return position;
}

function pause(state, suspend = true) {
    state.position = elapsed(state);
    if (state.native) state.native.pause();
    if (state.spcLoaded && suspend) spcBackend?.pause();
    if (state.node) { state.node.onended = null; state.node.stop(); state.node.disconnect(); state.node = null; }
    state.playing = false;
    clearInterval(state.timer);
    update(state);
}

function stop(state, suspend = true) {
    pause(state, suspend);
    state.controller.abort();
    state.controller = new AbortController();
    if (state.native) state.native.currentTime = 0;
    if (state.spcLoaded) {
        clearTimeout(spcBackend.timeoutID);
        if (spcBackend.scriptProcessorNode) {
            spcBackend.scriptProcessorNode.onaudioprocess = null;
            spcBackend.scriptProcessorNode.disconnect();
            spcBackend.scriptProcessorNode = null;
        }
        // Emscripten can abort after an invalid SPC; teardown must still finish.
        try { spcBackend.stopSPC(false); } catch { }
        state.spcLoaded = false;
    }
    state.position = 0;
    if (activePlayer === state) {
        activePlayer = null;
        if (suspend) audioContext?.suspend().catch(() => {});
    }
    update(state);
}

function update(state) {
    if (state.disposed) return;
    const position = elapsed(state);
    state.root.classList.toggle("is-playing", state.playing);
    state.play.innerHTML = `${state.playing ? pauseIcon : playIcon}<span>${state.playing ? "Pause" : "Play"}</span>`;
    state.play.setAttribute("aria-label", state.playing ? "Pause sound preview" : "Play sound preview");
    state.time.textContent = `${time(position)}${state.duration ? ` / ${time(state.duration)}` : ""}`;
    state.seek.max = String(state.duration || 1);
    state.seek.value = String(Math.min(position, state.duration || 0));
    state.seek.disabled = !state.ready || !state.duration || state.kind === "spc";
}

function time(value) { const seconds = Math.max(0, Math.floor(value || 0)); return `${Math.floor(seconds / 60)}:${String(seconds % 60).padStart(2, "0")}`; }

export function disposeAudioPreview(id) {
    const state = players.get(id);
    if (!state) return;
    stop(state);
    state.disposed = true;
    state.listeners.abort();
    state.controller.abort();
    if (state.native) { state.native.removeAttribute("src"); state.native.load(); }
    if (state.blobUrl) URL.revokeObjectURL(state.blobUrl);
    state.gain?.disconnect();
    state.bytes = state.samples = state.buffer = null;
    state.root.replaceChildren();
    players.delete(id);
}

new MutationObserver(() => {
    for (const [id, state] of players) if (!state.root.isConnected) disposeAudioPreview(id);
    if (activePlayer?.root.closest("[hidden]")) stop(activePlayer);
}).observe(document.documentElement, { childList: true, subtree: true, attributes: true, attributeFilter: ["hidden"] });
window.addEventListener("pagehide", () => { for (const id of [...players.keys()]) disposeAudioPreview(id); });
document.addEventListener("play", event => {
    if (activePlayer && event.target !== activePlayer.native && event.target instanceof HTMLMediaElement) stop(activePlayer);
}, true);
