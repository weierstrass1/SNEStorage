import { audioKind, nativeAudioTypes } from "./audio-formats.js?v=1";

const previews = new Map();
const maxPreviewBytes = 5 * 1024 * 1024;

export function releasePreview(key) {
    const gallery = previews.get(key);
    if (!gallery) return;
    for (const entry of gallery.entries.values()) releaseEntry(entry);
    previews.delete(key);
}

function releaseEntry(entry) {
    entry.controller.abort();
    if (entry.image) entry.image.removeAttribute("src");
    if (entry.url) URL.revokeObjectURL(entry.url);
}

function mediaType(file) {
    const mime = (file.fileType?.value ?? "").toLowerCase();
    const sound = audioKind(file.url ?? "", mime);
    if (sound) return { kind: sound, mime: mime || nativeAudioTypes.get((file.url ?? "").split(".").pop().toLowerCase()) || "" };
    if (mime.startsWith("image/")) return { kind: "image", mime };
    if (mime.startsWith("video/")) return { kind: "video", mime };
    const extension = (file.url ?? "").split(".").pop().toLowerCase();
    const types = {
        png: "image/png", jpg: "image/jpeg", jpeg: "image/jpeg",
        gif: "image/gif", webp: "image/webp", avif: "image/avif",
        mp4: "video/mp4", webm: "video/webm", ogv: "video/ogg", ogg: "video/ogg", mov: "video/quicktime"
    };
    const inferred = types[extension] ?? "";
    return { kind: inferred.split("/")[0] || "other", mime: inferred };
}

export async function readPreviewBlob(response, mime, signal) {
    if (Number(response.headers.get("Content-Length")) > maxPreviewBytes) {
        await response.body?.cancel();
        return null;
    }
    const reader = response.body?.getReader();
    if (!reader) return null;
    const parts = [];
    let size = 0;
    try {
        while (true) {
            signal.throwIfAborted();
            const { done, value } = await reader.read();
            if (done) break;
            size += value.byteLength;
            if (size > maxPreviewBytes) {
                await reader.cancel();
                return null;
            }
            parts.push(value);
        }
        return size > 0 ? new Blob(parts, { type: mime }) : null;
    } finally {
        reader.releaseLock();
    }
}

export function previewKind(file) { return mediaType(file ?? {}).kind; }

export function getReadyPreview(key, fileId) {
    return previews.get(key)?.entries.get(String(fileId))?.result ?? null;
}

// Keep only the current slide and its two neighbors. URLs never leave this page.
export async function loadPreview(metadataUrl, downloadUrl, fileId, key, playVideo,
    knownFile = null, neighbors = [], onImageReady = null, resourceId = 0, forceReload = false) {
    let gallery = previews.get(key);
    if (gallery && gallery.resourceId !== resourceId) {
        releasePreview(key);
        gallery = null;
    }
    if (!gallery) {
        gallery = { entries: new Map(), activeId: null, resourceId };
        previews.set(key, gallery);
    }
    const activeId = String(fileId);
    gallery.activeId = activeId;
    const retained = new Set([activeId, ...neighbors.map(item => String(item.fileId))]);
    for (const [id, entry] of gallery.entries) {
        if (!retained.has(id) || (id === activeId && forceReload)) {
            releaseEntry(entry);
            gallery.entries.delete(id);
        }
    }
    const active = { metadataUrl, downloadUrl, fileId, knownFile };
    const entry = getEntry(gallery, active, playVideo, "high");
    const result = await entry.promise;
    if (previews.get(key) !== gallery || gallery.activeId !== activeId)
        return { cancelled: true };
    if (result.status !== 200) return result;

    // Start neighbors after the main image is ready so it gets the bandwidth first.
    for (const neighbor of neighbors) {
        if (String(neighbor.fileId) === activeId ||
            mediaType(neighbor.knownFile ?? {}).kind !== "image") continue;
        const upcoming = getEntry(gallery, neighbor, false, "low");
        void upcoming.promise.then(response => {
            if (response.kind !== "image" || !response.url || typeof onImageReady !== "function" ||
                previews.get(key) !== gallery ||
                gallery.entries.get(String(neighbor.fileId)) !== upcoming) return;
            return onImageReady(neighbor.fileId, response.url);
        }).catch(() => { /* A discarded carousel no longer consumes this image. */ });
    }
    return result;
}

function getEntry(gallery, descriptor, playVideo, priority) {
    const id = String(descriptor.fileId);
    let entry = gallery.entries.get(id);
    if (entry && playVideo && !entry.playVideo) {
        releaseEntry(entry);
        gallery.entries.delete(id);
        entry = null;
    }
    if (entry) return entry;
    entry = { controller: new AbortController(), url: null, image: null, playVideo };
    gallery.entries.set(id, entry);
    entry.promise = fetchPreview(entry, descriptor, playVideo, priority).then(result => {
        entry.result = result;
        if (result.status !== 200 && gallery.entries.get(id) === entry) {
            releaseEntry(entry);
            gallery.entries.delete(id);
        }
        return result;
    });
    return entry;
}

// Metadata stays JSON; bytes always go through the existing access-checked action.
async function fetchPreview(entry, { metadataUrl, downloadUrl, fileId, knownFile }, playVideo, priority) {
    const signal = entry.controller.signal;
    const request = { credentials: "same-origin", signal, priority };
    try {
        let file = knownFile;
        // The detail response already includes each preview's File. Avoid loading it a second time.
        if (!file || String(file.id) !== String(fileId) || mediaType(file).kind === "other") {
            const metadata = await fetch(new URL(metadataUrl, document.baseURI), {
                ...request, headers: { Accept: "application/json" }
            });
            if (!metadata.ok) {
                if (metadata.status === 401) window.dispatchEvent(new Event("snestorage-session-rejected"));
                return { status: metadata.status, body: await metadata.text() };
            }
            file = await metadata.json();
        }
        if (!file || String(file.id) !== String(fileId)) return { status: 502 };
        const { kind, mime } = mediaType(file);
        if (["audio", "spc", "brr"].includes(kind))
            return { status: 200, kind, url: new URL(downloadUrl, document.baseURI).href };
        if (kind === "other" || (kind === "video" && !playVideo))
            return { status: 200, kind };

        // Bytes always go through the controller, which checks resource visibility and file ownership.
        const response = await fetch(new URL(downloadUrl, document.baseURI), {
            ...request, headers: { Accept: "*/*" }
        });
        if (!response.ok) {
            if (response.status === 401) window.dispatchEvent(new Event("snestorage-session-rejected"));
            return { status: response.status, body: await response.text() };
        }
        const blob = await readPreviewBlob(response, mime, signal);
        if (!blob) return { status: 413, message: "Previews must be nonempty and no larger than 5 MB." };
        signal.throwIfAborted();
        entry.url = URL.createObjectURL(blob);
        if (kind === "image") {
            entry.image = new Image();
            entry.image.decoding = "async";
            entry.image.src = entry.url;
            await entry.image.decode();
            signal.throwIfAborted();
        }
        return { status: 200, kind, url: entry.url };
    } catch (error) {
        if (signal.aborted || error.name === "AbortError") return { cancelled: true };
        if (error instanceof SyntaxError) return { status: 502 };
        if (error instanceof TypeError) return { status: 0 };
        return { status: 502, message: "This preview could not be decoded. Try another preview or retry." };
    }
}

window.addEventListener("pagehide", event => {
    if (event.persisted) return;
    for (const key of [...previews.keys()]) releasePreview(key);
});
