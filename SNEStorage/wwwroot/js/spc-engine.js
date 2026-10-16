// One emulator and AudioContext for local playlists and resource previews.
// Seek initialization adapts the included spc_player.js protocol:
// Copyright (C) 2021 Telinc1, LGPL-2.1-only. License/source are in ../lib/spc-player/.
export let audioContext;
let spcPromise, spcBackend, runtime;

export function contextForClick() {
    const Context = window.AudioContext || window.webkitAudioContext;
    if (!Context) throw new Error("Your browser does not support this sound player.");
    audioContext ??= new Context();
    // Called synchronously by a native Play click, before reading files or WASM.
    audioContext.resume().catch(() => {});
    return audioContext;
}

export function loadSpcEngine(context) {
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
                runtime = window.SNEStorageSPCModule;
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

// The upstream backend skips synchronously. Use its same load/connect protocol
// in short batches so a long seek can yield to the UI and be cancelled.
// Protocol based on the included LGPL spc_player.js (Telinc1, 2021).
export async function loadSpcAt(backend, bytes, seconds, isCurrent) {
    const target = Math.max(0, Math.floor(seconds));
    if (!isCurrent()) return false;
    if (!target) { backend.loadSPC(bytes, 0); return true; }
    if (!runtime || backend.status !== 1) throw new Error("The SPC engine is unavailable. Reload the page and try again.");
    stopSpcEngine(backend);
    backend.spcPointer = runtime._malloc(bytes.length);
    runtime.HEAPU8.set(bytes, backend.spcPointer);
    runtime._loadSPC(backend.spcPointer, bytes.length);
    for (let skipped = 0; skipped < target;) {
        if (!isCurrent()) return false;
        const amount = Math.min(10, target - skipped);
        runtime._skipSPC(amount);
        skipped += amount;
        await new Promise(resolve => setTimeout(resolve, 0));
    }
    if (!isCurrent()) return false;
    backend.playSPC();
    backend.scriptProcessorNode = backend.context.createScriptProcessor(backend.channelBuffers[0].length, 0, backend.channelBuffers.length);
    backend.scriptProcessorNode.onaudioprocess = backend.copyBuffers;
    backend.startedAt = backend.context.currentTime - target;
    backend.scriptProcessorNode.connect(backend.gainNode);
    backend.resume();
    return true;
}

export function stopSpcEngine(backend) {
    if (!backend) return;
    clearTimeout(backend.timeoutID);
    if (backend.scriptProcessorNode) {
        backend.scriptProcessorNode.onaudioprocess = null;
        backend.scriptProcessorNode.disconnect();
        backend.scriptProcessorNode = null;
    }
    // Invalid snapshots can abort Emscripten; cleanup still has to finish.
    try { backend.stopSPC(false); } catch { }
}
