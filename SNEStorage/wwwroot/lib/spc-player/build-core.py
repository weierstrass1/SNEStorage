"""Adapt @smwcentral/spc-player 2.0.2's minified dist/spc.js for SNEStorage.

SPDX-License-Identifier: LGPL-2.1-only
Upstream copyright (C) 2021 Telinc1; adaptation 2026-10-14.
"""
from pathlib import Path
import sys

if len(sys.argv) != 3:
    raise SystemExit("Usage: python build-core.py upstream/dist/spc.js spc-core.js")

source = Path(sys.argv[1]).read_text(encoding="utf-8")
backend_end = source.index("}}()})();", source.index("SMWCentral.SPCPlayer.Backend=")) + len("}}()})();")
runtime_start = source.index("var moduleOverrides=", backend_end)
core = source[:backend_end] + source[runtime_start:]
for original, replacement in [
    ('var Module=typeof Module!="undefined"?Module:{};', 'var Module=window.SNEStorageSPCModule||{};'),
    ('this.context=new AudioContext;', 'this.context=Module.audioContext||new AudioContext;')
]:
    if core.count(original) != 1:
        raise SystemExit("Unsupported upstream build: " + original)
    core = core.replace(original, replacement, 1)

banner = """/*!
 * SMW Central SPC player 2.0.2 — Copyright (C) 2021 Telinc1.
 * LGPL-2.1-only; see LICENSE and upstream-source.tar.gz in this directory.
 * SNEStorage adaptation (2026-10-14): remove floating UI; isolate Module;
 * reuse the AudioContext opened by the application's Play button.
 */
"""
Path(sys.argv[2]).write_text(banner + "(function(){\n" + core + "\n})();\n", encoding="utf-8")
