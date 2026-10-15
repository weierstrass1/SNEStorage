// Browser-only BPS/IPS application. BPS follows byuu's public-domain specification.
// No ROM code is executed. All file offsets, allocations and checksums are checked here.
export const patchLimits = Object.freeze({ rom: 32 * 1024 * 1024, patch: 64 * 1024 * 1024 });
const crcTable = new Uint32Array(256);
for (let n = 0; n < 256; n++) {
    let value = n;
    for (let bit = 0; bit < 8; bit++) value = value & 1 ? 0xedb88320 ^ value >>> 1 : value >>> 1;
    crcTable[n] = value >>> 0;
}
export function crc32(bytes) {
    let crc = 0xffffffff;
    for (const byte of bytes) crc = crcTable[(crc ^ byte) & 255] ^ crc >>> 8;
    return (crc ^ 0xffffffff) >>> 0;
}
export function hexCrc(value) { return value.toString(16).padStart(8, "0").toUpperCase(); }
const fail = message => { throw new Error(message); };
const u16 = (bytes, offset) => bytes[offset] | bytes[offset + 1] << 8;
const u24 = (bytes, offset) => bytes[offset] * 65536 + bytes[offset + 1] * 256 + bytes[offset + 2];
const u32 = (bytes, offset) => new DataView(bytes.buffer, bytes.byteOffset + offset, 4).getUint32(0, true);
const signature = (bytes, text) => [...text].every((character, i) => bytes[i] === character.charCodeAt(0));

function mirroredSum(bytes, offset = 0, length = bytes.length) {
    let power = 1;
    while (power * 2 <= length) power *= 2;
    let sum = 0;
    for (let i = offset; i < offset + power; i++) sum = (sum + bytes[i]) & 65535;
    if (length === power) return { sum, span: power };
    let remainder = mirroredSum(bytes, offset + power, length - power);
    while (remainder.span < power) remainder = { sum: remainder.sum * 2 & 65535, span: remainder.span * 2 };
    return { sum: (sum + remainder.sum) & 65535, span: power * 2 };
}

export function inspectRom(bytes) {
    if (!(bytes instanceof Uint8Array) || bytes.length < 32768 || bytes.length > patchLimits.rom + 512)
        fail("Choose a SNES ROM between 32 KB and 32 MB, with an optional 512-byte copier header.");
    const copierBytes = bytes.length % 32768 === 512 ? 512 : 0;
    const body = bytes.subarray(copierBytes);
    if (body.length % 32768 !== 0 || body.length > patchLimits.rom)
        fail("The file size does not match complete SNES ROM banks. Compressed, split and interleaved dumps are not supported.");
    const candidates = [
        [0x7fc0, "LoROM", [0x20, 0x22, 0x23]],
        [0xffc0, "HiROM", [0x21, 0x2a]],
        [0x407fc0, "ExLoROM", [0x20, 0x22]],
        [0x40ffc0, "ExHiROM", [0x25]]
    ];
    let best;
    for (const [offset, mapping, modes] of candidates) {
        if (offset + 64 > body.length || !modes.includes(body[offset + 21] & 0xef)) continue;
        const reset = u16(body, offset + 60), sizeCode = body[offset + 23], ramCode = body[offset + 24];
        if (reset < 0x8000 || reset === 0xffff || sizeCode < 5 || sizeCode > 15 || ramCode > 13) continue;
        const titleBytes = body.subarray(offset, offset + 21);
        const visible = [...titleBytes].filter(byte => byte > 32 && byte < 127 || byte >= 0xa1 && byte <= 0xdf).length;
        const printable = [...titleBytes].every(byte => byte === 0 || byte === 255 || byte >= 32 && byte < 127 || byte >= 0xa1 && byte <= 0xdf);
        const pairValid = (u16(body, offset + 28) ^ u16(body, offset + 30)) === 65535;
        if (!(printable && visible >= 3) && !pairValid) continue;
        const score = (pairValid ? 5 : 0) + (printable && visible >= 3 ? 3 : 0) + (offset < 0x400000 || body.length > 0x400000 ? 1 : 0);
        if (!best || score > best.score) best = { offset, mapping, pairValid, score, titleBytes };
    }
    if (!best) fail("No recognizable SNES ROM header was found. Choose an uncompressed .sfc or .smc ROM, not a renamed file.");
    const title = new TextDecoder("shift_jis").decode(best.titleBytes).replace(/[\x00-\x1f\x7f-\x9f\ufffd]/g, "").trim() || "Untitled SNES ROM";
    return {
        title, mapping: best.mapping, copierBytes, bytes: bytes.length,
        crc: hexCrc(crc32(bytes)),
        checksumValid: best.pairValid && mirroredSum(body).sum === u16(body, best.offset + 30)
    };
}

function bpsReader(bytes) {
    const end = bytes.length - 12;
    let cursor = 4;
    return {
        get cursor() { return cursor; }, end,
        skip(count) { if (!Number.isSafeInteger(count) || count < 0 || cursor + count > end) fail("The BPS patch is truncated."); cursor += count; },
        literal(count) { const start = cursor; this.skip(count); return bytes.subarray(start, cursor); },
        number() {
            let result = 0, shift = 1;
            for (let i = 0; i < 8; i++) {
                if (cursor >= end) fail("The BPS patch contains a truncated number.");
                const value = bytes[cursor++];
                result += (value & 127) * shift;
                if (!Number.isSafeInteger(result)) fail("The BPS patch contains an out-of-range number.");
                if (value & 128) return result;
                shift *= 128;
                result += shift;
                if (!Number.isSafeInteger(result)) break;
            }
            fail("The BPS patch contains an invalid number.");
        }
    };
}

export function inspectPatch(bytes, extension) {
    if (!bytes.length || bytes.length > patchLimits.patch) fail("Choose a nonempty patch no larger than 64 MB.");
    if (extension === "bps" && signature(bytes, "BPS1")) {
        if (bytes.length < 19) fail("The BPS patch is incomplete.");
        if (crc32(bytes.subarray(0, -4)) !== u32(bytes, bytes.length - 4)) fail("The BPS patch checksum is invalid. Download the patch again.");
        const reader = bpsReader(bytes);
        const sourceSize = reader.number(), targetSize = reader.number(), metadataSize = reader.number();
        if (sourceSize < 32768 || targetSize < 32768 || sourceSize > patchLimits.rom + 512 || targetSize > patchLimits.rom + 512)
            fail("This BPS patch does not target a SNES ROM within the supported 32 MB limit.");
        reader.skip(metadataSize);
        return { format: "BPS", sourceSize, targetSize, sourceCrc: u32(bytes, bytes.length - 12), targetCrc: u32(bytes, bytes.length - 8) };
    }
    if (extension === "ips" && signature(bytes, "PATCH")) {
        const size = scanIps(bytes, 0);
        return { format: "IPS", targetSize: size };
    }
    fail("The patch contents do not match its .bps or .ips extension. Extract the patch from its archive first.");
}

function scanIps(bytes, sourceSize, target = null) {
    let cursor = 5, size = sourceSize, work = 0;
    while (cursor + 3 <= bytes.length) {
        const offset = u24(bytes, cursor);
        cursor += 3;
        if (offset === 0x454f46) {
            if (bytes.length - cursor === 3) {
                size = u24(bytes, cursor);
                cursor += 3;
            }
            if (cursor !== bytes.length) fail("The IPS patch has unsupported data after its end marker.");
            if (size > patchLimits.rom + 512) fail("The patched ROM exceeds the supported size.");
            return size;
        }
        if (cursor + 2 > bytes.length) break;
        let length = bytes[cursor] * 256 + bytes[cursor + 1];
        cursor += 2;
        const rle = length === 0;
        let value;
        if (rle) {
            if (cursor + 3 > bytes.length) break;
            length = bytes[cursor] * 256 + bytes[cursor + 1];
            value = bytes[cursor + 2]; cursor += 3;
            if (!length) fail("The IPS patch contains an empty repeat record.");
        } else if (cursor + length > bytes.length) break;
        const end = offset + length;
        if (end > patchLimits.rom + 512 || (work += length) > patchLimits.rom * 8)
            fail("The IPS patch exceeds this browser tool's processing limits.");
        size = Math.max(size, end);
        if (target) {
            const count = Math.max(0, Math.min(end, target.length) - offset);
            if (count) {
                if (rle) target.fill(value, offset, offset + count);
                else target.set(bytes.subarray(cursor, cursor + count), offset);
            }
        }
        if (!rle) cursor += length;
    }
    fail("The IPS patch is truncated or missing its EOF marker.");
}

export function selectSource(original, romInfo, patch, mode = "auto") {
    if (!["auto", "keep", "remove", "add"].includes(mode)) fail("Choose a supported copier-header option.");
    const candidates = [{ bytes: original, note: "The ROM's copier header was kept as selected." }];
    const needsAlternative = patch.format === "BPS" || mode === "remove" || mode === "add";
    if (needsAlternative && romInfo.copierBytes) candidates.push({ bytes: original.subarray(512), note: "The 512-byte copier header was removed." });
    else if (needsAlternative) {
        const added = new Uint8Array(original.length + 512);
        added.set(original, 512);
        candidates.push({ bytes: added, note: "A blank 512-byte copier header was added." });
    }
    const chosen = mode === "remove" ? candidates.find(candidate => candidate.bytes.length < original.length)
        : mode === "add" ? candidates.find(candidate => candidate.bytes.length > original.length) : candidates[0];
    if (!chosen) fail(mode === "remove" ? "This ROM has no 512-byte copier header to remove." : "This ROM already has a copier header.");
    if (patch.format !== "BPS") return { ...chosen, note: mode === "auto" || mode === "keep" ? "The ROM was kept as selected. IPS cannot verify its game or revision." : chosen.note };
    const matching = (mode === "auto" ? candidates : [chosen]).find(candidate =>
        candidate.bytes.length === patch.sourceSize && crc32(candidate.bytes) === patch.sourceCrc);
    if (matching) return { ...matching, note: matching === candidates[0] ? "BPS base ROM CRC32 verified." : matching.note + " BPS base ROM CRC32 verified." };
    if (candidates.some(candidate => candidate.bytes.length === patch.targetSize && crc32(candidate.bytes) === patch.targetCrc)) fail("This ROM already matches the patched version.");
    fail(`This BPS patch requires a different base ROM (CRC32 ${hexCrc(patch.sourceCrc)}). Check the game, region, revision and patch instructions.`);
}

export function applyPatch(source, bytes, patch) {
    if (patch.format === "IPS") {
        const size = scanIps(bytes, source.length);
        if (size < 32768) fail("This IPS patch would not produce a complete SNES ROM.");
        const target = new Uint8Array(size);
        target.set(source.subarray(0, size));
        scanIps(bytes, source.length, target);
        return target;
    }
    const reader = bpsReader(bytes);
    reader.number(); reader.number(); reader.skip(reader.number());
    const target = new Uint8Array(patch.targetSize);
    let output = 0, sourceRelative = 0, targetRelative = 0;
    while (reader.cursor < reader.end) {
        const command = reader.number(), action = command % 4, length = Math.floor(command / 4) + 1;
        if (length > target.length - output) fail("The BPS patch writes beyond its declared output size.");
        if (action === 0) {
            if (length > source.length - output) fail("The BPS patch reads beyond the base ROM.");
            target.set(source.subarray(output, output + length), output);
        } else if (action === 1) target.set(reader.literal(length), output);
        else {
            const relative = reader.number();
            const delta = Math.floor(relative / 2) * (relative % 2 ? -1 : 1);
            if (action === 2) {
                sourceRelative += delta;
                if (!Number.isSafeInteger(sourceRelative) || sourceRelative < 0 || length > source.length - sourceRelative) fail("The BPS patch has an invalid source-copy offset.");
                target.set(source.subarray(sourceRelative, sourceRelative + length), output);
                sourceRelative += length;
            } else {
                targetRelative += delta;
                if (!Number.isSafeInteger(targetRelative) || targetRelative < 0 || targetRelative >= output) fail("The BPS patch refers to output that has not been written yet.");
                // Copy sequentially: BPS explicitly permits overlapping target-copy records.
                for (let i = 0; i < length; i++) target[output + i] = target[targetRelative++];
            }
        }
        output += length;
    }
    if (output !== target.length || crc32(target) !== patch.targetCrc) fail("The patched ROM failed BPS output verification. No file was downloaded.");
    return target;
}
