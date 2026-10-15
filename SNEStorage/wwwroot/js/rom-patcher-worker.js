import { inspectRom, inspectPatch, selectSource, applyPatch, patchLimits, hexCrc } from "./rom-patcher-core.js?v=1";

// Files are structured-cloned into this browser worker, never posted to an HTTP endpoint.
self.onmessage = async ({ data }) => {
    try {
        const { romFile, patchFile, mode, action } = data;
        let rom, romInfo, patchBytes, patch;
        if (romFile) {
            if (!/\.(sfc|smc)$/i.test(romFile.name) || romFile.size > patchLimits.rom + 512)
                throw new Error("Choose an uncompressed .sfc or .smc ROM no larger than 32 MB.");
            self.postMessage({ type: "progress", message: "Checking SNES ROM…" });
            rom = new Uint8Array(await romFile.arrayBuffer());
            romInfo = inspectRom(rom);
        }
        if (patchFile) {
            if (!/\.(bps|ips)$/i.test(patchFile.name) || patchFile.size > patchLimits.patch)
                throw new Error("Choose a .bps or .ips patch no larger than 64 MB.");
            self.postMessage({ type: "progress", message: "Checking patch…" });
            patchBytes = new Uint8Array(await patchFile.arrayBuffer());
            patch = inspectPatch(patchBytes, patchFile.name.split(".").pop().toLowerCase());
        }
        const source = rom && patch ? selectSource(rom, romInfo, patch, mode) : null;
        const summary = {
            rom: romInfo, patch: patch ? { format: patch.format,
                sourceCrc: patch.sourceCrc === undefined ? null : hexCrc(patch.sourceCrc) } : null,
            note: source?.note ?? "", ready: Boolean(source)
        };
        if (action !== "apply") { self.postMessage({ type: "checked", summary }); return; }
        if (!source) throw new Error("Choose both a ROM and a patch first.");
        self.postMessage({ type: "progress", message: "Applying patch and verifying the result…" });
        const output = applyPatch(source.bytes, patchBytes, patch);
        const outputInfo = inspectRom(output);
        self.postMessage({ type: "complete", summary, outputInfo, buffer: output.buffer }, [output.buffer]);
    } catch (error) {
        self.postMessage({ type: "error", message: error instanceof Error ? error.message : "The patch could not be applied." });
    }
};
