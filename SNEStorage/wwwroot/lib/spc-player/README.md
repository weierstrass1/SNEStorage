# SPC emulator used by SNEStorage

Upstream: [SMW Central SPC player](https://github.com/telinc1/smwcentral-spc-player),
published as `@smwcentral/spc-player` **2.0.2**. Its repository has since moved to
[Codeberg](https://codeberg.org/Telinc1/smwcentral-spc-player).

Source revision: `dbe113fd87f47ce3f2da5852d1704928bbb75a53` (the npm package's
`gitHead`). Copyright (C) 2021 Telinc1. The SNES emulator is Blargg's snes_spc,
Copyright (C) 2003–2006 Shay Green; resampling also credits Liam Wilson.

## Files and changes

- `spc.wasm`: unchanged emulator binary from the published package.
- `spc-core.js`: Emscripten runtime and upstream audio backend, adapted on
  2026-10-14. The legacy floating player UI was removed; its `Module` is private
  to an IIFE and receives configuration through `window.SNEStorageSPCModule`.
  The backend accepts the AudioContext opened by SNEStorage's Play button.
- `upstream-source.tar.gz`: corresponding upstream source, including C/C++,
  readable JavaScript and the original `make.sh` build recipe.
- `build-core.py`: reproduces the JavaScript adaptation from upstream `dist/spc.js`.
- `LICENSE`: GNU Lesser General Public License, version 2.1. Keep the license,
  copyright notices, source and build instructions with distributed copies.

The upstream source permits LGPL 2.1 or later; npm labels the distribution
LGPL-2.1-only. These files retain the LGPL 2.1 license and original notices.
The adapted backend still uses ScriptProcessorNode. It does not use AudioWorklet.

## Rebuilding

Extract `upstream-source.tar.gz`. With Emscripten and the upstream npm build
dependencies installed, run `npm install` and `./make.sh` in that source directory.
The original recipe emits `dist/spc.js` and `dist/spc.wasm`.

Apply this adapter to the **2.0.2 minified distribution**:

```powershell
python build-core.py PATH/TO/dist/spc.js spc-core.js
```

The script deliberately fails if upstream boundaries change. A build using a
different Emscripten version/minifier may require updating those boundaries. Use
the JS and WASM from the same build; copy the resulting WASM here alongside the JS.
An existing matching distribution can be obtained from:

- [npm package 2.0.2](https://registry.npmjs.org/@smwcentral/spc-player/-/spc-player-2.0.2.tgz)
- [source revision archive](https://github.com/telinc1/smwcentral-spc-player/archive/dbe113fd87f47ce3f2da5852d1704928bbb75a53.tar.gz)

SNEStorage's preview controls are in `wwwroot/js/audio-preview.js`. Local playlists
use `wwwroot/js/spc-playlist.js` and IndexedDB through `spc-library.js`.
`spc-engine.js` shares the AudioContext/emulator loader and cleanup between both;
its cooperative seek adapts the load/connect protocol in the included upstream
`spc_player.js` (Copyright (C) 2021 Telinc1, LGPL-2.1-only) and uses the unchanged
WASM skip routine in bounded chunks. This keeps the UI responsive to cancellation.
`audio-focus.js` coordinates ownership so a preview cannot run over the playlist.
BRR prediction math
in `wwwroot/js/brr-decoder.js` is adapted from the included `SPC_DSP.cpp` and
retains its LGPL notice. Files are hosted locally; playback makes no CDN requests.
