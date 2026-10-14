// SPDX-License-Identifier: LGPL-2.1-or-later
// BRR prediction/rounding follows Blargg's SPC_DSP.cpp (Copyright (C) 2003-2006 Shay Green).
// Reference source and license: /lib/spc-player/upstream-source.tar.gz and /lib/spc-player/LICENSE.
// SNEStorage adaptation: bounded, one-shot Float32 decoding, no instrument/ADSR emulation.
import { inspectBrr } from "./audio-formats.js?v=1";

export async function decodeBrr(bytes, signal) {
    const info = inspectBrr(bytes);
    const samples = new Float32Array(info.blocks * 16);
    let previous = 0, previous2 = 0, output = 0;
    for (let block = 0; block < info.blocks; block++) {
        if (block % 4096 === 0) {
            signal?.throwIfAborted();
            // Large samples must not monopolize the page's event loop.
            if (block > 0) await new Promise(resolve => setTimeout(resolve, 0));
        }
        const start = info.offset + block * 9, header = bytes[start];
        const shift = header >> 4, filter = header >> 2 & 3;
        for (let i = 0; i < 16; i++) {
            const packed = bytes[start + 1 + (i >> 1)];
            let value = i % 2 === 0 ? packed >> 4 : packed & 15;
            if (value >= 8) value -= 16;
            let sample = shift <= 12 ? (value << shift) >> 1 : value < 0 ? -2048 : 0;
            const older = previous2 >> 1;
            if (filter === 1) sample += (previous >> 1) + ((-previous) >> 5);
            if (filter === 2) sample += previous - older + (older >> 4) + ((-3 * previous) >> 6);
            if (filter === 3) sample += previous - older + ((3 * older) >> 4) + ((-13 * previous) >> 7);
            sample = Math.max(-32768, Math.min(32767, sample));
            sample = sample * 2 & 65535;
            if (sample >= 32768) sample -= 65536;
            previous2 = previous;
            previous = sample;
            samples[output++] = sample / 32768;
        }
    }
    return { ...info, samples };
}
