const patchers = new Map();

export function initializePatcher(id) {
    disposePatcher(id);
    const root = document.getElementById(id);
    if (!root) return;
    const find = name => root.querySelector(`[data-patcher-${name}]`);
    const events = new AbortController();
    const state = { root, events, worker: null, timer: null, url: null, ready: false,
        rom: find("rom"), patch: find("patch"), mode: find("header"), apply: find("apply"),
        cancel: find("cancel"), status: find("status"), error: find("error"),
        result: find("result"), download: find("download"), info: find("info"), note: find("note"), warning: find("warning") };
    patchers.set(id, state);
    const on = (element, name, handler) => element.addEventListener(name, handler, { signal: events.signal });
    for (const input of [state.rom, state.patch, state.mode]) on(input, "change", () => run(state, "inspect"));
    on(state.apply, "click", () => run(state, "apply"));
    on(state.cancel, "click", () => {
        stopWorker(state);
        state.status.textContent = "Cancelled. Your original files have not been changed.";
        state.apply.disabled = !state.ready;
    });
    state.apply.disabled = true;
    find("unavailable").hidden = true;
}

function stopWorker(state) {
    state.worker?.terminate();
    state.worker = null;
    clearTimeout(state.timer);
    state.timer = null;
    state.cancel.hidden = true;
    state.root.setAttribute("aria-busy", "false");
}

function clearDownload(state) {
    state.result.hidden = true;
    state.download.removeAttribute("href");
    if (state.url) URL.revokeObjectURL(state.url);
    state.url = null;
}

function run(state, action) {
    stopWorker(state);
    clearDownload(state);
    state.error.hidden = true;
    if (action !== "apply") {
        state.info.hidden = state.warning.hidden = true;
        state.note.textContent = "";
        state.ready = false;
    }
    state.apply.disabled = true;
    const romFile = state.rom.files?.[0], patchFile = state.patch.files?.[0];
    if (!romFile && !patchFile) { state.status.textContent = "Choose a ROM and its patch to begin."; return; }
    state.status.textContent = "Checking files…";
    state.root.setAttribute("aria-busy", "true");
    let worker;
    try { worker = new Worker(new URL("./rom-patcher-worker.js?v=1", import.meta.url), { type: "module" }); }
    catch { showError(state, "This browser could not start the patcher. Try a current browser with JavaScript enabled."); return; }
    state.worker = worker;
    state.cancel.hidden = false;
    state.timer = setTimeout(() => showError(state, "Processing took too long and was stopped. Check your files and try again."), 60000);
    worker.onerror = event => {
        event.preventDefault();
        if (state.worker === worker) showError(state, "The patcher could not finish. Try again or choose another patch.");
    };
    worker.onmessage = ({ data }) => {
        if (state.worker !== worker || !state.root.isConnected) return;
        if (data.type === "progress") { state.status.textContent = data.message; return; }
        if (data.type === "error") { showError(state, data.message); return; }
        if (data.type !== "checked" && data.type !== "complete") return;
        stopWorker(state);
        state.ready = data.summary.ready;
        state.apply.disabled = !state.ready;
        showSummary(state, data.summary);
        if (data.type === "checked") {
            state.status.textContent = state.ready ? "Ready to apply the patch." : "Choose the remaining file to continue.";
            return;
        }
        const extension = data.outputInfo.copierBytes ? ".smc" : ".sfc";
        const basename = patchFile.name.replace(/\.(bps|ips)$/i, "")
            .replace(/[<>:"/\\|?*\x00-\x1f\x7f]/g, "-").replace(/[. ]+$/g, "").slice(0, 140) || "patched-rom";
        const filename = basename + extension;
        state.url = URL.createObjectURL(new Blob([data.buffer], { type: "application/octet-stream" }));
        state.download.href = state.url;
        state.download.download = filename;
        state.download.textContent = `Download ${filename}`;
        state.result.hidden = false;
        state.status.textContent = "Patch applied successfully. Your patched ROM is ready.";
        state.result.querySelector("[data-patcher-output]").textContent =
            `${data.outputInfo.mapping} · ${(data.buffer.byteLength / 1048576).toLocaleString(undefined, { maximumFractionDigits: 2 })} MB · CRC32 ${data.outputInfo.crc}`;
        state.download.click();
    };
    try { worker.postMessage({ action, romFile, patchFile, mode: state.mode.value }); }
    catch { showError(state, "The browser could not read these files. Select them again and retry."); }
}

function showSummary(state, summary) {
    const info = summary.rom;
    if (info) {
        state.info.hidden = false;
        const set = (key, value) => state.info.querySelector(`[data-patcher-${key}]`).textContent = value;
        set("title", info.title);
        set("mapping", info.mapping);
        set("copier", info.copierBytes ? "512 bytes" : "None");
        set("crc", info.crc);
        state.warning.hidden = info.checksumValid;
    }
    state.note.textContent = summary.patch?.format === "IPS"
        ? `${summary.note || "IPS patch recognized."} Check the patch's instructions for the correct game, region, revision and copier-header requirements.`
        : summary.note || (summary.patch ? "BPS patch checksum verified. Choose a ROM to check compatibility." : "");
}

function showError(state, message) {
    stopWorker(state);
    state.ready = false;
    state.apply.disabled = true;
    state.status.textContent = "Your original files have not been changed.";
    state.error.textContent = message;
    state.error.hidden = false;
}

export function disposePatcher(id) {
    const state = patchers.get(id);
    if (!state) return;
    stopWorker(state);
    clearDownload(state);
    state.events.abort();
    patchers.delete(id);
}

new MutationObserver(() => {
    for (const [id, state] of patchers) if (!state.root.isConnected) disposePatcher(id);
}).observe(document.documentElement, { subtree: true, childList: true });
window.addEventListener("pagehide", () => { for (const id of [...patchers.keys()]) disposePatcher(id); });
