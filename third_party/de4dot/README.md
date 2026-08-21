# de4dot 3.1.41592.3405 (binary distribution)

`de4dot.zip` — the .NET deobfuscator, version 3.1.41592.3405, licensed **GPLv3**.
Source code: https://github.com/de4dot/de4dot (the license text ships inside the zip).

SHA256: `C726CBD18B894CA63B7F6A565C6C86EF512B96E68119C6502CDF64A51F6A1C78`

Vendored so `tools/prepare-game.ps1` can regenerate `game/EXAPUNKS-deob.exe` (the module's
compile-time reference — see CLAUDE.md "Typed game access") on any machine with the game
installed, with no network dependency. The VERSION IS A PIN, not a preference: de4dot's
generated names (`method_N`, `gclass52_N`) are the identifiers the module's source code is
written in, and a different de4dot version could name members differently and break the
build. Reproducibility of this exact version is verified: a fresh run over the shipping exe
produces a byte-identical namemap.

This is a standalone developer TOOL. Nothing from it ships with the mod, and it never
touches game files outside the local `game/` analysis workspace.
