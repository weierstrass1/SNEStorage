// Keep these browser limits aligned with FileService. Files never leave the browser here.
export const uploadLimits = Object.freeze({
    mediaBytes: 5 * 1024 * 1024,
    archiveBytes: 250 * 1024 * 1024,
    previews: 20,
    imagePixels: 4194304,
    gifFramePixels: 229376,
    imageFrames: 1800
});

const imageTypes = new Map([
    ["png", "image/png"], ["jpg", "image/jpeg"], ["jpeg", "image/jpeg"],
    ["gif", "image/gif"], ["webp", "image/webp"], ["avif", "image/avif"]
]);
const videoTypes = new Map([["mp4", "video/mp4"], ["webm", "video/webm"], ["ogv", "video/ogg"], ["ogg", "video/ogg"]]);
const archiveExtensions = new Set(["zip", "rar", "7z", "tar", "gz"]);
const inspections = new WeakMap();

function extension(file) { return file.name.split(".").pop().toLowerCase(); }

export function basicUploadError(file, context) {
    if (!file) return context === "archive" ? "Choose a resource archive." : "Choose an image or video.";
    if (!file.size) return "This file is empty. Choose another file.";
    const maxBytes = context === "archive" ? uploadLimits.archiveBytes : uploadLimits.mediaBytes;
    if (file.size > maxBytes) return context === "archive"
        ? "The archive must be no larger than 250 MB."
        : "Each avatar or preview must be no larger than 5 MB.";
    const ext = extension(file);
    if (context === "archive") return archiveExtensions.has(ext) ? ""
        : "Choose a ZIP, RAR, 7Z, TAR, GZ or TAR.GZ archive.";
    const mime = imageTypes.get(ext) ?? (context === "preview" ? videoTypes.get(ext) : null);
    if (!mime) return context === "avatar" ? "Choose a PNG, JPG, GIF, WebP or AVIF image."
        : "Choose a PNG, JPG, GIF, WebP, AVIF, MP4, WebM or OGV preview.";
    if (file.type.trim().toLowerCase() !== mime) return "The file's reported format does not match its extension. Choose another file.";
    return "";
}

export async function validateUpload(file, context) {
    const basicError = basicUploadError(file, context);
    if (basicError || context === "archive") return basicError;
    // File objects are immutable. Reuse the same local inspection at selection and submit.
    if (!inspections.has(file)) inspections.set(file, inspectMedia(file).catch(() =>
        "This file could not be read. Choose a complete, valid image or video."));
    return inspections.get(file);
}

function dimensionsError(width, height, gif = false) {
    if (width <= 0 || height <= 0) return "This image has invalid dimensions.";
    if (width * height > (gif ? uploadLimits.gifFramePixels : uploadLimits.imagePixels))
        return gif ? "GIFs can have at most 229,376 pixels per frame (for example, 512 × 448)."
            : "Images can have at most 4,194,304 pixels (for example, 2048 × 2048).";
    return "";
}

async function inspectMedia(file) {
    if (videoTypes.has(extension(file))) return inspectVideo(file);
    const bytes = new Uint8Array(await file.arrayBuffer());
    const info = imageInfo(bytes, file.type);
    if (info) {
        const error = dimensionsError(info.width, info.height, file.type === "image/gif");
        if (error) return error;
        if (info.frames > uploadLimits.imageFrames)
            return "Animated images can contain at most 1,800 frames. Choose a shorter animation.";
    }
    if (!info && await avifFrameCount(bytes) > uploadLimits.imageFrames)
        return "Animated images can contain at most 1,800 frames. Choose a shorter animation.";
    const image = new Image();
    return readMedia(file, image, "load", () => dimensionsError(image.naturalWidth, image.naturalHeight, file.type === "image/gif"));
}

function imageInfo(bytes, mime) {
    const view = new DataView(bytes.buffer, bytes.byteOffset, bytes.byteLength);
    const text = (offset, count) => String.fromCharCode(...bytes.subarray(offset, offset + count));
    if (mime === "image/gif") return gifInfo(bytes, view, text);
    if (mime === "image/png") {
        if (bytes.length < 33 || bytes[0] !== 137 || text(1, 3) !== "PNG" || text(12, 4) !== "IHDR") throw new Error("Invalid PNG");
        const info = { width: view.getUint32(16), height: view.getUint32(20), frames: 1 };
        for (let offset = 8; offset + 12 <= bytes.length;) {
            const size = view.getUint32(offset);
            if (offset + 12 + size > bytes.length) throw new Error("Truncated PNG");
            if (text(offset + 4, 4) === "acTL" && size === 8) info.frames = view.getUint32(offset + 8);
            offset += size + 12;
        }
        if (!info.frames) throw new Error("Invalid frame count");
        return info;
    }
    if (mime === "image/jpeg") {
        if (bytes[0] !== 255 || bytes[1] !== 216) throw new Error("Invalid JPEG");
        let offset = 2;
        while (offset < bytes.length) {
            if (bytes[offset++] !== 255) throw new Error("Invalid JPEG marker");
            while (bytes[offset] === 255) offset++;
            const marker = bytes[offset++];
            if (marker === 217 || marker === 218) break;
            if (marker === 1 || (marker >= 208 && marker <= 215)) continue;
            const size = view.getUint16(offset);
            if (size < 2 || offset + size > bytes.length) throw new Error("Truncated JPEG");
            if ([192, 193, 194, 195, 197, 198, 199, 201, 202, 203, 205, 206, 207].includes(marker))
                return { width: view.getUint16(offset + 5), height: view.getUint16(offset + 3), frames: 1 };
            offset += size;
        }
        throw new Error("JPEG dimensions not found");
    }
    if (mime === "image/webp") {
        if (text(0, 4) !== "RIFF" || text(8, 4) !== "WEBP") throw new Error("Invalid WebP");
        let width = 0, height = 0, frames = 0;
        const uint24 = offset => bytes[offset] | bytes[offset + 1] << 8 | bytes[offset + 2] << 16;
        for (let offset = 12; offset + 8 <= bytes.length;) {
            const kind = text(offset, 4), size = view.getUint32(offset + 4, true), data = offset + 8;
            if (data + size > bytes.length) throw new Error("Truncated WebP");
            if (kind === "VP8X" && size >= 10) { width = uint24(data + 4) + 1; height = uint24(data + 7) + 1; }
            if (kind === "VP8 " && size >= 10 && !width) {
                width = view.getUint16(data + 6, true) & 16383;
                height = view.getUint16(data + 8, true) & 16383;
            }
            if (kind === "VP8L" && size >= 5 && !width) {
                width = 1 + (bytes[data + 1] | (bytes[data + 2] & 63) << 8);
                height = 1 + (bytes[data + 2] >> 6 | bytes[data + 3] << 2 | (bytes[data + 4] & 15) << 10);
            }
            if (kind === "ANMF") frames++;
            offset = data + size + (size % 2);
        }
        return { width, height, frames: Math.max(1, frames) };
    }
    // AVIF dimensions are read by the browser below; frame metadata is optional.
    return null;
}

async function avifFrameCount(bytes) {
    if (typeof ImageDecoder === "undefined" || !await ImageDecoder.isTypeSupported("image/avif")) return 0;
    const decoder = new ImageDecoder({ data: bytes, type: "image/avif", preferAnimation: true });
    let timer;
    try {
        await Promise.race([
            Promise.all([decoder.tracks.ready, decoder.completed]),
            new Promise((_, reject) => { timer = setTimeout(() => reject(new Error("Metadata timeout")), 10000); })
        ]);
        return decoder.tracks.selectedTrack?.frameCount ?? 0;
    } catch {
        // Optional metadata support must not reject images the browser can otherwise display.
        return 0;
    } finally {
        clearTimeout(timer);
        decoder.close();
    }
}

function gifInfo(bytes, view, text) {
    if (bytes.length < 14 || !["GIF87a", "GIF89a"].includes(text(0, 6))) throw new Error("Invalid GIF");
    const width = view.getUint16(6, true), height = view.getUint16(8, true);
    // Reject large GIF canvases before parsing frames or asking the browser to decode.
    if (dimensionsError(width, height, true)) return { width, height, frames: 1 };
    let offset = 13, frames = 0;
    if (bytes[10] & 128) offset += 3 * (1 << ((bytes[10] & 7) + 1));
    const skipBlocks = () => {
        while (offset < bytes.length) {
            const size = bytes[offset++];
            if (!size) return;
            offset += size;
        }
        throw new Error("Truncated GIF block");
    };
    while (offset < bytes.length) {
        const kind = bytes[offset++];
        if (kind === 59) {
            if (!frames) throw new Error("Empty GIF");
            return { width, height, frames };
        }
        if (kind === 33) { offset++; skipBlocks(); continue; }
        if (kind !== 44 || offset + 9 > bytes.length) throw new Error("Invalid GIF frame");
        const packed = bytes[offset + 8];
        offset += 9;
        if (packed & 128) offset += 3 * (1 << ((packed & 7) + 1));
        offset++; // LZW minimum code size; only skip data, never expand it here.
        skipBlocks();
        frames++;
        if (frames > uploadLimits.imageFrames) return { width, height, frames };
    }
    throw new Error("Truncated GIF");
}

function inspectVideo(file) {
    const video = document.createElement("video");
    // A valid container can use a codec this browser cannot inspect. Leave that case to FileService.
    if (!video.canPlayType(file.type)) return "";
    video.preload = "metadata";
    video.muted = true;
    return readMedia(file, video, "loadedmetadata", () => video.videoWidth > 0 && video.videoHeight > 0 ? ""
        : "This video has invalid dimensions. Choose another video.");
}

function readMedia(file, media, event, inspect) {
    return new Promise(resolve => {
        const url = URL.createObjectURL(file);
        let timer, settled = false;
        const loaded = () => finish(inspect());
        const finish = message => {
            if (settled) return;
            settled = true;
            clearTimeout(timer);
            media.removeEventListener(event, loaded);
            media.onload = media.onloadedmetadata = media.onerror = null;
            if (media instanceof HTMLVideoElement) media.pause();
            media.removeAttribute("src");
            if (media instanceof HTMLVideoElement) media.load();
            URL.revokeObjectURL(url);
            resolve(message);
        };
        media.addEventListener(event, loaded, { once: true });
        media.onerror = () => finish("This file could not be opened. Choose a complete, valid image or video.");
        timer = setTimeout(() => finish("Checking this file took too long. Select it again or choose another file."), 10000);
        media.src = url;
    });
}
