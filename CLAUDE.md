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
- **Boot click-gate**: the loading screen blocks until a mouse click (SDL event type
  1025, `MOUSEBUTTONDOWN`) before init returns and the first screen appears — game
  behavior; nothing ticks until then (our tick prefix included). THE MOD FIXES THIS:
  `module/src/Patches/SplashPatches.cs` announces a localized "Press any key to
  continue" when the gate opens and advances it on any key press. Mechanics (from the
  decompile): the gate waits on LOCALS (nothing to poke) but accepts any type-1025 SDL
  event; its event loop DISCARDS key events, so `SDL_GetKeyboardState` is the only key
  sensor there; we postfix the per-frame splash draw (the unique `void(float,bool)` on
  `LoadingScreenRenderer`), edge-detect keys against a baseline, and `SDL_PushEvent` a
  synthetic 1025 — the game then runs its own click path (music, sfx, fade) identically
  to a real mouse click. Armed by `LoadingScreenRenderer` ordinal-2 postfix, disarmed by
  `GameLogic` ordinal-24 prefix, so it can never fire during gameplay.
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

User install (the future installer) = copy 6 files into the game folder —
`EXAPUNKS.exe.config`, `ExaAccess.dll`, `ExaAccess.Module.dll`, `0Harmony.dll`,
`prism.dll`, `steam_appid.txt` — plus the `ExaAccess\locale\` folder (the locale
tables). Uninstall = delete the config. The config binds the
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
- `src/Speech/` — the WrathAccess handler stack, host-side because engines hold
  native/OS resources that must survive module reloads: `Tts.cs` (the facade/chokepoint),
  `SpeechManager` (priority chain Prism → SAPI → clipboard; lazy Detect/Load; auto
  fallback — never strand a blind user voiceless), `PrismHandler`+`PrismNative`,
  `SapiHandler` (in-box System.Speech — replaces WotR's 500 lines of manual COM),
  `ClipboardHandler` (raw user32). Output choice: `speech.output` in
  `%LOCALAPPDATA%\ExaAccess\settings.json` (flat dotted-key JSON, `src/HostConfig.cs`)
  or the `EXAACCESS_SPEECH` env var for dev runs; default auto.
- `src/Dev/` — DEBUG-only dev server (+ `/reload`); `src/Log.cs` — file logger.

Module (each reload starts this half cold — statics are per-load):
- `module/src/ExaAccessModule.cs` — `IModModule` implementation, module composition
  root: loc first, then patches/hooks/FrameLoop steps; speaks "ExaAccess ready." once
  at boot (generation 1 only — the splash prompt is the next thing the user hears).
- `module/src/Localization/` — the WrathAccess loc layer: `Loc.T`, lazy `Message` with
  `{var}` substitution, `LocalizationManager` (enGB fallback manifest + per-frame
  language poll via the pluggable `LanguageSource`; game-language mapping is future
  work). Tables load from `<game>\ExaAccess\locale\<lang>\<table>.json` — rooted off
  the HOST dll (the module is byte-loaded, it has no disk location). Module-side so a
  hot reload re-reads the JSON: edit a string, build/copy, /reload, hear it.
- `module/src/FrameLoop.cs` — ordered, defensive per-frame step registry (steps:
  keyboard snapshot → input → loc poll → screens). `FrameClock.cs` — Stopwatch clock.
- `module/src/UI/` — `ScreenNames` (locale-backed labels + obfuscation filter),
  `ScreenAnnouncer`, `ControlTypes` (the role-word/type registry).
- `module/src/UI/Graph/` — the WrathAccess KEY-GRAPH CORE, ported verbatim (BCL-pure by
  design — keep it that way; its 900+ lines of tests came over verbatim too): ControlId
  two-tier identity, GraphTypes (nodes/vtables/stops/regions/parent chains), KeyGraph
  (moves, Tab-stops, regions, trees, focus reconcile-on-rebuild), GraphBuilder
  (menu/raw modes, groups, position stamping), GraphAnnouncer (path-diff speech;
  wording hooks localized in module Load). Immediate-mode: screens re-declare controls
  each render; focus persists by ControlId.
- `module/src/Input/` — WrathAccess input substrate over SDL: `SdlKeyboard` (per-frame
  snapshot of `SDL_GetKeyboardState` — no obfuscated members needed, independent of
  what the game consumes), `Scancode`, `SdlKeyboardBinding` (exact-match modifiers),
  `InputAction`/`InputBinding`/`InputCategory`/`InputManager` (category priority +
  chord shadowing + OS typematic repeat via `OsKeyboard`). The navigator/screen-stack
  hooks are pluggable funcs (`UiDispatcher`, `ActiveCategoriesProvider`,
  `SuppressPoll`) — wired when GraphNavigator/ScreenManager land with the first screen.
- `module/src/Game/SdlNative.cs` — P/Invoke over the game's own SDL2.dll: keyboard
  state reads + `SDL_PushEvent` (56-byte union — the struct is Size=64 on purpose).
- `module/src/Patches/SplashPatches.cs` — the any-key splash advance (see "Boot
  click-gate" above).

## Hot reload (DEBUG loop for feature work)
The module is `Assembly.Load(byte[])`'d, so `dotnet build module/ExaAccess.Module.csproj`
works with the game RUNNING (the Debug build auto-deploys it), then `POST /reload` (or
F6 in-game) swaps it in — no restart, no click-gate. Load-then-swap: a broken build
leaves the old module running. Old copies leak until exit (dev cost only). The REPL
resets on reload so `/eval` sees the new types. Host/contract changes still need a full
game restart (the host is file-locked and loaded once). Rules for module code are in
`src/Modularity/IModModule.cs` — notably: module Harmony patches use a per-load unique
id + `UnpatchSelf` in Dispose, and native handles live host-side only.
`/eval` gotcha: after a reload, every module generation is still loaded, and a bare
type reference (`Loc.T(...)`) may bind to an OLD generation. When it matters, resolve
through the newest copy:
`AppDomain.CurrentDomain.GetAssemblies().Where(a => a.GetName().Name == "ExaAccess.Module").Last()`.

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
- **Localize every string the mod speaks** — `Loc.T(key[, args])` / `Message` + an
  entry in `module/assets/locale/enGB/ui.json` (the complete translation manifest;
  another language = a dropped-in folder under `<game>\ExaAccess\locale\`). Named
  `{placeholders}`, not string.Format. Exemptions: HOST emergency strings (spoken when
  the module itself failed to load — localization is module-side) and dev-only tooling.
- **Speech never interrupts by default** (SayTheSpire house preference), and all
  user-facing output flows through `Tts.Speak` — the single chokepoint (it also feeds
  the dev `/speech` tap).
- **All dev tooling is `#if DEBUG` and loopback-only.** Release builds ship zero dev
  surface (no server, no Mono.CSharp, no speech tap).
- **`AssemblyVersion` stays 1.0.0.0** unless `deploy/EXAPUNKS.exe.config` is updated in
  the same commit.
- **Keep `module/src/UI/Graph` BCL-pure** (no game/SDL/host-dev references) — its test
  suite compiles it standalone in spirit; purity is what made the WotR port free.

## Roadmap
1. **(done)** In-process injection, Harmony on obfuscated members, Prism speech,
   dev server, screen-change announcements.
2. **(done)** Zero-loader: stock `EXAPUNKS.exe` boots the mod via AppDomainManager
   config; mod ships as a DLL.
3. **(done)** Host/module split with hot reload (build + /reload, no restart).
4. **(done)** WrathAccess localization layer + handler-chain speech stack
   (Prism → SAPI → clipboard); every module string localized.
5. **(done)** Boot click-gate: localized "Press any key to continue" + any-key advance.
6. **(done)** UI graph core + input substrate ported (with the WotR test suites).
7. First screen: port GraphNavigator + Screen/ScreenManager against the EXAPUNKS
   screen stack, wire `InputManager.UiDispatcher`/`ActiveCategoriesProvider`, add a
   focus-mode key-suppression story (EXAPUNKS has no Keyboard.Disabled lever — swallow
   keys via the game's key-set facade or SDL), then model the title/menu screen.
8. Map the remaining obfuscated transition/overlay screens to friendly names.
9. Read the model: `Sim`/`SimExa`/`SimHost`/`Register`/`SimFile` for gameplay, the EXA
   code editor for program text — this game is text-centric, a strong a11y target.
10. Type-ahead search (WotR's TypeAheadSearch is pure — port with SDL TEXTINPUT), the
    settings tree, and the mod menu.
11. Installer (6 files + locale folder; uninstall = delete the config).

