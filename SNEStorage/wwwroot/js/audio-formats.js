export const nativeAudioTypes = new Map([
    ["mp3", "audio/mpeg"], ["wav", "audio/wav"], ["ogg", "audio/ogg"],
    ["oga", "audio/ogg"], ["opus", "audio/ogg"], ["flac", "audio/flac"],
    ["m4a", "audio/mp4"], ["aac", "audio/aac"], ["weba", "audio/webm"]
]);

export function audioKind(filename, mime = "") {
    const ext = filename.split(/[?#]/)[0].split(".").pop().toLowerCase();
    if (ext === "spc") return "spc";
    if (ext === "brr") return "brr";
    if (ext === "ogg" && mime.startsWith("video/")) return null;
    return nativeAudioTypes.has(ext) || mime.startsWith("audio/") ? "audio" : null;
}

export function normalizeAudioFile(file) {
    const kind = audioKind(file.name, file.type);
    if (!kind) return file;
    const ext = file.name.split(".").pop().toLowerCase();
    const mime = kind === "spc" ? "audio/x-spc" : kind === "brr" ? "audio/x-brr" : nativeAudioTypes.get(ext);
    return mime && mime !== file.type ? new File([file], file.name, { type: mime, lastModified: file.lastModified }) : file;
}

export function inspectSpc(bytes) {
    const signature = "SNES-SPC700 Sound File Data ";
    if (bytes.length < 0x10180 || signature.split("").some((character, index) => bytes[index] !== character.charCodeAt(0)))
        throw new Error("Choose a complete SPC snapshot containing the SNES audio state.");
    const text = (offset, size) => new TextDecoder("windows-1252").decode(bytes.subarray(offset, offset + size)).split("\0")[0].trim();
    if (bytes[0x23] !== 0x1a) return { title: "", game: "", duration: null };
    const time = text(0xa9, 3);
    const duration = /^\d{1,3}$/.test(time) ? Number(time) :
        bytes[0xa9] | bytes[0xaa] << 8 | bytes[0xab] << 16;
    return { title: text(0x2e, 32), game: text(0x4e, 32), duration: duration > 0 && duration <= 3600 ? duration : null };
}

export function inspectBrr(bytes) {
    const offset = bytes.length % 9 === 0 ? 0 : bytes.length % 9 === 2 ? 2 : -1;
    if (offset < 0 || bytes.length - offset < 9) throw new Error("BRR samples must contain complete 9-byte blocks, optionally preceded by a 2-byte loop offset.");
    const blocks = (bytes.length - offset) / 9;
    for (let block = 0; block < blocks - 1; block++) {
        if (bytes[offset + block * 9] & 1) throw new Error("This BRR file contains data after its final sample block.");
    }
    const header = bytes[bytes.length - 9];
    if (!(header & 1)) throw new Error("This BRR sample is missing its final block flag.");
    const loop = Boolean(header & 2);
    const loopOffset = offset === 2 ? bytes[0] | bytes[1] << 8 : 0;
    if (loop && offset === 2 && (loopOffset % 9 !== 0 || loopOffset >= blocks * 9))
        throw new Error("This BRR sample has an invalid loop offset.");
    return { offset, blocks, loop, loopSample: loop ? loopOffset / 9 * 16 : 0, hasLoopOffset: offset === 2 };
}
