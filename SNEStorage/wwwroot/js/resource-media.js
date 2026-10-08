const previews = new Map();
const maxPreviewBytes = 5 * 1024 * 1024;

export function releasePreview(key) {
    const preview = previews.get(key);
    if (!preview) return;
    preview.controller.abort();
    if (preview.url) URL.revokeObjectURL(preview.url);
    previews.delete(key);
}

function mediaType(file) {
    const mime = (file.fileType?.value ?? "").toLowerCase();
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

async function readPreview(response, mime, signal) {
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

// File metadata stays JSON. Bytes come from the existing protected download action.
// Only the active preview is retained; video bytes are requested only after Play.
export async function loadPreview(metadataUrl, downloadUrl, fileId, key, playVideo, knownFile = null) {
    releasePreview(key);
    const preview = { controller: new AbortController(), url: null };
    previews.set(key, preview);
    const signal = preview.controller.signal;
    const request = { credentials: "same-origin", signal };
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
        const blob = await readPreview(response, mime, signal);
        if (!blob) return { status: 413, message: "Previews must be nonempty and no larger than 5 MB." };
        signal.throwIfAborted();
        if (previews.get(key) !== preview) return { cancelled: true };
        preview.url = URL.createObjectURL(blob);
        return { status: 200, kind, url: preview.url };
    } catch (error) {
        if (signal.aborted || error.name === "AbortError") return { cancelled: true };
        if (error instanceof SyntaxError) return { status: 502 };
        if (error instanceof TypeError) return { status: 0 };
        throw error;
    }
}
