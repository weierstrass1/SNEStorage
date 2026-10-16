// ID666/xid6 timing fields: https://snesmusic.org/files/spc_file_format.txt
// Metadata describes playback limits; it cannot identify a musical ending itself.
export function readSpcTiming(bytes) {
    const view = new DataView(bytes.buffer, bytes.byteOffset, bytes.byteLength);
    const decoder = new TextDecoder("windows-1252");
    const text = (offset, size) => decoder.decode(bytes.subarray(offset, offset + size)).split("\0")[0].trim();
    const result = { duration: null, fadeDuration: 0, introDuration: 0, loopDuration: null, endDuration: 0, tagLoops: null, timingVersion: 2 };
    if (bytes[0x23] === 0x1a) {
        const length = text(0xa9, 3), date = text(0x9e, 11);
        const textFormat = /^\d{2,3}$/.test(length) || /^\d{1,3}$/.test(length) &&
            (date.includes("/") || bytes[0xb0] === 0 && bytes[0xb1] !== 0);
        const seconds = textFormat ? Number(length) : bytes[0xa9] | bytes[0xaa] << 8 | bytes[0xab] << 16;
        const fadeText = text(0xac, 5);
        const fade = textFormat ? (/^\d+$/.test(fadeText) ? Number(fadeText) : 0) : view.getUint32(0xac, true);
        result.fadeDuration = fade >= 0 && fade <= 120000 ? fade / 1000 : 0;
        if (seconds > 0 && seconds <= 21600) result.duration = seconds + result.fadeDuration;
    }
    // A full snapshot ends at 0x10200; do not scan audio RAM for apparent tags.
    const start = 0x10200;
    if (bytes.length < start + 8 || text(start, 4) !== "xid6") return result;
    const size = view.getUint32(start + 4, true);
    if (size > bytes.length - start - 8) return result;
    const end = start + 8 + size;
    let intro, loop, ending, fade, loops;
    for (let offset = start + 8; offset + 4 <= end;) {
        const id = bytes[offset], type = bytes[offset + 1], length = view.getUint16(offset + 2, true);
        offset += 4;
        if (type && length > end - offset) break;
        if (id === 0x35 && type === 0) loops = Math.min(length, 255);
        if (type === 4 && length === 4) {
            const signed = id === 0x32 ? view.getInt32(offset, true) : view.getUint32(offset, true);
            const seconds = signed / 64000;
            if (Math.abs(seconds) <= 6000) {
                if (id === 0x30) intro = seconds;
                if (id === 0x31) loop = seconds;
                if (id === 0x32) ending = seconds;
                if (id === 0x33) fade = seconds;
            }
        }
        offset += type ? length : 0;
        // Accommodate older writers which omitted the required zero padding.
        const aligned = (offset + 3) & ~3;
        if (aligned <= end && bytes.subarray(offset, aligned).every(value => value === 0)) offset = aligned;
    }
    if (fade !== undefined) {
        if (result.duration !== null) result.duration += fade - result.fadeDuration;
        result.fadeDuration = fade;
    }
    if (loop > 0 || intro > 0) {
        result.introDuration = intro ?? 0;
        result.loopDuration = loop > 0 ? loop : null;
        result.endDuration = ending ?? 0;
        result.tagLoops = loops ?? 1;
        const total = result.introDuration + (loop ?? 0) * result.tagLoops + result.endDuration + result.fadeDuration;
        if (total > 0 && Number.isFinite(total)) result.duration = total;
    }
    return result;
}
