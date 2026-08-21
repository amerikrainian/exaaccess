# ExaAccess — Accessibility Mod for EXAPUNKS

Screen-reader accessibility mod for blind players. Speaks screens today; grows into a
model-driven reader of the desktop, EXA code editor, and simulation via Prism. Sibling
to WrathAccess (`../wotr-access`) and SayTheSpire — reuse those patterns where they fit.

## Game facts
- **Engine**: NOT Unity (despite shipping SDL2). `EXAPUNKS.exe` (internal name
  `Burbank`) is one self-contained **.NET Framework 4.x, x64** assembly running
  Zachtronics' own SDL2 + Direct3D11 engine via P/Invoke. No BepInEx/Doorstop/UMM
  machinery applies — plain Harmony on a plain managed exe.
- **Install**: `C:\Program Files (x86)\Steam\steamapps\common\EXAPUNKS`
- **Steam**: appid **716490**; the Steam client must be running. `GameLogic` init calls
  `SteamAPI.RestartAppIfNecessary`, so `steam_appid.txt` (716490) sits in the game
  folder (build writes it; Bootstrap re-writes as fallback). Without it a non-Steam
  launch restarts through Steam — cosmetic double launch; the relaunch still loads us.
- **Injection**: `deploy/EXAPUNKS.exe.config` next to the game names
  `ExaAccess.Bootstrap` as the process **AppDomainManager** → the CLR instantiates the
  mod inside the **stock exe** before any game code (even static ctors) runs. No game
  file is modified ("verify integrity" ignores extra files); deleting the config =
  vanilla. There is no loader exe anymore; `ExaAccess.dll` is the whole mod.
- **Obfuscation**: Eazfuscator.NET. The shipping exe keeps real TYPE names
  (`GameLogic`, `IScreen`, `DesktopScreen`, `Sim`, …) but MEMBERS are `#=q…` gibberish.
  See "Analysis workspace" for how we target them anyway.
- **Boot click-gate**: the loading screen blocks until a mouse click (SDL
  `MOUSEBUTTONDOWN`) before init returns and the first screen appears — game behavior,
  not a bug. Nothing ticks until then (our tick prefix included).
- **Harmony**: vendored **2.4.2** net48 (`third_party/harmony/`) — the game ships no
  Harmony of its own (unlike WotR, where the game's 2.0.4 pins the API).
- **Target framework**: `net48`, `PlatformTarget=x64` (must match the game exactly).
  Needs the .NET Framework 4.8 targeting pack to build.

## Analysis workspace: `game/` (gitignored — NEVER commit or ship any of it)
- `game/EXAPUNKS.orig.exe` — pristine copy of the shipping exe.
- `game/EXAPUNKS-deob.exe` — de4dot output: readable member names (`method_8`,
  `class5_0`), decrypted strings.
- `game/decompiled/` — `ilspycmd -p` dump of the deob exe (~480 files). Read this.
- `game/Steamworks.NET.dll`, `Ionic.Zip.Reduced.dll` — game deps copied so tools resolve.
- **The fact that makes targeting work**: de4dot does NOT preserve metadata tokens, but
  it DOES preserve each type's member ORDER. Its `method_N` is exactly the N-th method
  in MetadataToken order, and the same ordinal selects the same member on the shipping
  exe. So `GameState` resolves **types by real name, members by token-order ordinal**,
  cross-checked by signature (fields pinned by unique type first, ordinal as fallback)
  so a game update fails loudly instead of patching the wrong member.
- Known ordinals (`GameLogic`): method[8]=init, method[25]=per-frame tick,
  field[0]=static self-reference, field[21]=`Class5<IScreen>` screen stack (top = last
  element; its single `List<>` field is matched by type, not name).
- **Re-analyzing** (after a game update — none in years): run de4dot on a copy of the
  shipping exe → `game/EXAPUNKS-deob.exe`, then `ilspycmd -p` into `game/decompiled/`
  (`~\.dotnet\tools\ilspycmd.exe`; de4dot downloads live outside the repo —
  `tools/de4dot/` is gitignored as a guard). Then re-verify every ordinal.

## Build & deploy
```
dotnet build
```
Building the solution (repo root) builds host + module + tests. A Debug build deploys
into the game folder: `ExaAccess.dll`, `ExaAccess.Module.dll`, `0Harmony.dll`,
`prism.dll` (native screen-reader bridge), `EXAPUNKS.exe.config`, `Mono.CSharp.dll` (dev
REPL), writes `steam_appid.txt`, and deletes any stale pre-DLL `ExaAccess.exe`.
The HOST dll copy needs the game closed (file-locked; the deploy warns and continues);
the MODULE dll deploys fine with the game running — that's the hot-reload loop.
`dotnet build -c Release` compiles without deploying and contains zero dev tooling.
Override the install path with `-p:GameDir="…"`.

Tests: `dotnet test` from the repo root (`ExaAccess.sln` = mod + `tests/ExaAccess.Tests`, xunit on
net48, InternalsVisibleTo). Game-independent logic (resolution, text mapping, loc, UI graph core)
belongs there — grow the suite with each subsystem.

User install (the future installer) = copy 6 files into the game folder:
`EXAPUNKS.exe.config`, `ExaAccess.dll`, `ExaAccess.Module.dll`, `0Harmony.dll`,
`prism.dll`, `steam_appid.txt`. Uninstall = delete the config. The config binds the
host assembly by **full display name**, so the host's `AssemblyVersion` is pinned at
**1.0.0.0** in its csproj — bump both in lockstep or the mod silently stops loading
(release versioning goes in FileVersion instead).

## Logs
`%LOCALAPPDATA%\ExaAccess\exaaccess.log` — fresh file per launch; the one path to give
testers. The stock exe is a GUI app, so console output goes nowhere — the file is the
only log surface.

## Dev loop (DEBUG builds)
Set `EXAACCESS_DEV=1` (or drop a `devserver.enable` marker file in the game folder) and
launch `EXAPUNKS.exe`. A loopback HTTP server comes up on `127.0.0.1:8772`
(`EXAACCESS_DEV_PORT` overrides; WotR uses 8771 — keep them distinct):

| Route | Purpose |
|---|---|
| `GET /health` | liveness |
| `POST /say` | speak the body through the real speech path |
| `GET /speech?since=N` | read back what was spoken (we can't hear TTS) |
| `GET /screen` | active screen + full stack, by type name |
| `POST /eval` | C# against the live game, main thread, persistent REPL state |

`/eval` and `/screen` run on the game's main thread (queued, pumped from the tick
prefix); `/say` and `/speech` answer directly off the HTTP thread. Reach live game
state from `/eval` via `ExaAccess.GameState`.

**Click-gate in automation**: nothing ticks (and `/screen`/`/eval` time out) until the
gate gets a click. Post one: find the EXAPUNKS window HWND, `PostMessage`
`WM_LBUTTONDOWN`(0x201) + `WM_LBUTTONUP`(0x202) with REAL client coordinates packed in
lParam (e.g. `(100 << 16) | 100`) — lParam 0, i.e. (0,0), does NOT release the gate.

## Architecture (current): permanent HOST + reloadable MODULE
Two assemblies (pattern ported from NonVisualCalculus). The HOST (`ExaAccess.dll`,
root `src/`) is the config-bound permanent half; the MODULE (`ExaAccess.Module.dll`,
`module/src/`) holds every feature and hot-reloads (see "Hot reload" below).

Host:
- `src/Bootstrap.cs` — THE entry point: `AppDomainManager` the CLR instantiates inside
  the stock exe. Pins cwd, writes `steam_appid.txt`, boots speech, binds `GameState`,
  attaches the host Harmony patches, loads the module, starts the dev server, hooks
  `ProcessExit`. Owns `TickFrame` (dev pump → `Module.Tick`, read fresh each frame) and
  `ReloadModule`. Swallows every exception — see Hard rules.
- `src/GameState.cs` + `src/MemberResolver.cs` — reflection-cached view of the live
  game; the ordinal/signature resolution primitive (unit-tested).
- `src/Patches/GameLogicPatches.cs` — the two host hooks: init postfix (sets
  GameInitialized) and tick prefix (calls `Bootstrap.TickFrame`). Applied ONCE, never
  unpatched — they are the module's heartbeat. Attached manually via reflected
  `MethodInfo`s (no `typeof(GameLogic)` at compile time).
- `src/Modularity/` — `IModModule`/`ModHost` contract + `ModuleLoader`
  (byte-load, load-then-swap; the module dll is never file-locked).
- `src/Speech/` — `PrismNative.cs` P/Invoke over `prism.dll`; `Tts.cs` facade. Host-side
  because the native backend handle must survive module reloads.
- `src/Dev/` — DEBUG-only dev server (+ `/reload`); `src/Log.cs` — file logger.

Module (each reload starts this half cold — statics are per-load):
- `module/src/ExaAccessModule.cs` — `IModModule` implementation, module composition
  root: registers FrameLoop steps, announces readiness (generation 1 only).
- `module/src/FrameLoop.cs` — ordered, defensive per-frame step registry.
- `module/src/UI/` — `ScreenNames` (labels + obfuscation filter), `ScreenAnnouncer`.

## Hot reload (DEBUG loop for feature work)
The module is `Assembly.Load(byte[])`'d, so `dotnet build module/ExaAccess.Module.csproj`
works with the game RUNNING (the Debug build auto-deploys it), then `POST /reload` (or
F6 in-game) swaps it in — no restart, no click-gate. Load-then-swap: a broken build
leaves the old module running. Old copies leak until exit (dev cost only). The REPL
resets on reload so `/eval` sees the new types. Host/contract changes still need a full
game restart (the host is file-locked and loaded once). Rules for module code are in
`src/Modularity/IModModule.cs` — notably: module Harmony patches use a per-load unique
id + `UnpatchSelf` in Dispose, and native handles live host-side only.

## Hard rules
- **Never commit or ship game code.** `game/` (deob exe, decompiled source, copied game
  dlls) is a local analysis workspace only, and stays gitignored. We ship exactly the
  5-file install set above — nothing derived from the game's binaries.
- **Never crash the game.** Every Harmony hook body catches all exceptions; `Bootstrap`
  swallows everything (an exception escaping `InitializeNewDomain` kills the process
  before the game starts); speech is optional. Whatever breaks, the user still gets
  their vanilla game.
- **Read the model, never the pixels** — and route every game read through `GameState`,
  so obfuscation-resolution stays in one file.
- **Resolve types by real name, members by token-order ordinal, and cross-check** by
  signature (or pin fields by unique type). Never bake de4dot names (`method_8`) or raw
  metadata tokens into the mod — both are meaningless on the shipping exe.
- **Never speak `#=q…`** — filter obfuscated names (`GameLogicPatches.IsObfuscated`)
  before anything reaches `Tts`; map screens to friendly labels as they're identified.
- **Speech never interrupts by default** (SayTheSpire house preference), and all
  user-facing output flows through `Tts.Speak` — the single chokepoint (it also feeds
  the dev `/speech` tap). Localization is deferred until WrathAccess's Loc layer is
  ported; until then keep speakable strings few and easy to sweep.
- **All dev tooling is `#if DEBUG` and loopback-only.** Release builds ship zero dev
  surface (no server, no Mono.CSharp, no speech tap).
- **`AssemblyVersion` stays 1.0.0.0** unless `deploy/EXAPUNKS.exe.config` is updated in
  the same commit.

