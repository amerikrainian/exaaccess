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
- **Re-analyzing** (after a game update — none in years): `tools\prepare-game.ps1
  -Force -Decompile` regenerates orig + deob + `game/decompiled/` (de4dot is vendored
  and pinned; ilspycmd via `dotnet tool install -g ilspycmd`). Then re-verify every
  ordinal and the NameMap anchors (the module build fails loudly if alignment broke).
- **Translating an obfuscated `#=q…` TYPE to its decompiled name** (types work like
  members: row ORDER is preserved): get the live index via `/eval`
  (`asm.ManifestModule.GetTypes().OrderBy(t => t.MetadataToken)`, IndexOf), then
  `dotnet run --project tools/TypeMap -- game/EXAPUNKS-deob.exe` prints deob TypeDef
  rows as `index<TAB>name`. Calibrate the offset with name-preserving anchors
  (GetTypes omits `<Module>` and de4dot strips obfuscator types): THIS build,
  deob row = live index − 14 (GameLogic 1205→1191; verified DesktopScreen 1148→1134,
  LocString 1296→1282). NEVER reflection-load the deob exe (initializers stack-overflow).
  (For code the full deob↔shipping map exists anyway — see "Typed game access" below;
  TypeMap remains the quick one-off lookup tool.)

## Typed game access (the remap pipeline)
The MODULE compiles directly against `game/EXAPUNKS-deob.exe` (readable de4dot names;
`Private=false`, never shipped — and a hard BUILD PREREQUISITE: no deob exe, no module
build). Both binaries carry assembly name "Burbank", so references bind to the loaded
shipping game at runtime once names are fixed up:

- `tools/NameMap` (runs from the module build, incremental) aligns orig vs deob —
  two-pointer walk over TypeDef rows, junk types skipped by shape, per-type members
  zipped with the same skip logic — and emits `namemap.tsv` (deob → shipping renames;
  ~10.5k entries). It FAILS the build unless every real-name anchor pairs exactly and
  the skip count matches; a game update dies here loudly instead of misbinding.
- The host's `Modularity/GameRefRemapper` (Mono.Cecil) rewrites the module BYTES at
  load (the same seam hot reload uses): member refs first via a METHOD-BODY OPERAND
  walk — Cecil materializes a fresh reference per call site for members on
  generic-instance parents (`GClass52<bool>::method_0`), so renaming the
  GetMemberReferences() table view silently misses those — then type refs. Missing
  namemap.tsv = module load fails loudly.
- So module source reads like WrathAccess: `GameLogic.gameLogic_0.method_12(screen)`,
  `S.gclass52_5.method_2(v)`, `new GClass21(cell)` — compiler-checked, IntelliSense'd.
- The two escape hatches: PRIVATE members can't compile — resolve by deob name via
  `Game/Deobf` (reads the same namemap; works on RENAMED types too — the map's T rows
  translate the runtime shipping type name back to the deob name the M/F rows key by);
  Harmony targets for PUBLIC methods via `Game/Expr.MethodOf(() => …)` so the ldtoken
  remaps (STRING-based reflection with deob names does NOT remap — never use it).
- **Title screen decoded** (deob `GClass368`, live type index 1055): the room scene.
  Per-frame `imethod_1(float)` draws save-progress overlays (gated by
  `saveData_0.method_13("ghast-1", 0)`-style story keys) and THREE mouse hotspots via
  its `method_0` helper (hover fade + two LocString labels, click = act):
  Sawayama Z7 computer → `GameLogic.method_12(new DesktopScreen())`;
  TEC Constellation II tablet → `new ControlPanelScreen()` — ALSO opened by the
  game's own Escape key handling; TRASH WORLD NEWS (only once "ghast-1" is done) →
  `new GClass214(0)`. So: `GameLogic.method_12(IScreen)` = screen PUSH,
  `GameLogic.method_6()` = active screen. The game's own localization is
  `GClass7.smethod_5(key, …) → LocString` — labels are localized game-side.
- **Desktop decoded** (`DesktopScreen`, name-preserved; `game/decompiled/DesktopScreen.cs`):
  a FIXED layout (no window system, nothing draggable) drawn immediate-mode — AXIOM
  organizer (task list + detail pane + typed action button), CHATSUBO (chat log + the
  10-character user roster; Chat/Tasks tabs appear after story flag "ember-7"),
  right-edge program launchers (solitaire/sandbox/arcade/custom, unlock-gated) + close.
  Campaign data: `GClass61.gclass290_0` (main) / `gclass290_1` (side jobs), each
  `.list_0` of `CampaignItem` — ALL PUBLIC: locString_0/1/2 = title/date/description,
  string_0 = id, genum20_0 = type (0 puzzle→EditorScreen, 1 cutscene, 3 solitaire,
  5 arcade, 6 custom), maybe_0 = hostname/file, method_0() = completed,
  method_6() = the action-button LocString ("PLAY CUTSCENE", …). Selection is GLOBAL:
  `GameLogic.campaignItem_0`. Desktop privates (via `Deobf`): method_9(item) = select
  (the game's own arrow/click path), method_11() = open selected (the double-click
  path), list_0 = chat log (`List<GClass24>`), bool_2 = Chat/Tasks flag, tuple_0 =
  roster. Task reveal/complete markers accumulate in `GameLogic.hashSet_0`
  (GClass37 = task appeared, GClass38 = completed). Chat display names:
  `Vignette.dictionary_0`; text through `Vignette.smethod_0` (profanity mask) + the
  `<INSERT_RUN_TO_INSTRUCTION_KEY_COMBO>` platform substitution. THE GAME READS THE
  KEYBOARD HERE (arrows/Enter/Home/End/PgUp/PgDn drive task selection) through its
  input facade `GClass64` — pure per-frame snapshot reads, safe to prefix-skip:
  smethod_17(key) = just-pressed (Enter/Escape/Home/End; smethod_15/16 compose over
  it), smethod_22(key) = pressed-with-repeat (arrows + numpad aliases),
  smethod_23(GEnum1 0–3) = left/right/up/down actions over smethod_22.

## Build & deploy
```
dotnet build
```
Building the solution (repo root) builds host + module + tests. BUILD PREREQUISITE:
`game/EXAPUNKS-deob.exe` (the module compiles against the deob names and the build
generates `namemap.tsv` — see "Typed game access"). Fresh-machine setup is ONE command
with the game installed: `tools\prepare-game.ps1` — it copies the orig exe from the
Steam dir, extracts the VENDORED de4dot (third_party/de4dot, version-pinned
3.1.41592.3405, SHA-checked; the pin matters — de4dot's generated names ARE the
module's source identifiers, and this version is verified to reproduce a byte-identical
namemap), and runs it. `-Force` refreshes after a game update; `-Decompile` also
rebuilds `game/decompiled/` (needs ilspycmd). The build itself auto-copies the orig exe
when missing; only the deob half needs the script. A machine without a game install
still cannot build the module.
A Debug build deploys into the game folder: `ExaAccess.dll`, `ExaAccess.Module.dll`,
`Mono.Cecil.dll` (the remapper), `0Harmony.dll`, `prism.dll` (native screen-reader
bridge), `EXAPUNKS.exe.config`, `Mono.CSharp.dll` (dev REPL), `ExaAccess\namemap.tsv`
+ `ExaAccess\locale\`, writes `steam_appid.txt`, and deletes any stale pre-DLL
`ExaAccess.exe`.
The HOST dll copy needs the game closed (file-locked; the deploy warns and continues);
the MODULE dll deploys fine with the game running — that's the hot-reload loop.
`dotnet build -c Release` compiles without deploying and contains zero dev tooling.
Override the install path with `-p:GameDir="…"`.

Tests: `dotnet test` from the repo root (`ExaAccess.sln` = mod + `tests/ExaAccess.Tests`, xunit on
net48, InternalsVisibleTo). Game-independent logic (resolution, text mapping, loc, UI graph core)
belongs there — grow the suite with each subsystem.

User install (the future installer) = copy 7 files into the game folder —
`EXAPUNKS.exe.config`, `ExaAccess.dll`, `ExaAccess.Module.dll`, `Mono.Cecil.dll`,
`0Harmony.dll`, `prism.dll`, `steam_appid.txt` — plus the `ExaAccess\` folder
(`namemap.tsv` + `locale\`). Uninstall = delete the config. (namemap.tsv ships name
pairs only — the same information our ordinals always encoded; no game code ships.) The config binds the
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
| `GET /gui` | reflection dump of the active screen model — fields in TOKEN ORDER as `f[N]` (lines up with de4dot names/ordinals), collections expanded, depth-capped |
| `GET /screenshot` | capture the game window to `%LOCALAPPDATA%\ExaAccess\screenshots\*.png`, path returned. In-process PrintWindow(PW_RENDERFULLCONTENT): NO focus change, works occluded, full render resolution. NEVER SetForegroundWindow for captures |
| `POST /eval` | C# against the live game, main thread, persistent REPL state (C# 6 — no pattern matching) |

`/eval` and `/screen` run on the game's main thread (queued, pumped from the tick
prefix); `/say` and `/speech` answer directly off the HTTP thread. Reach live game
state from `/eval` via `ExaAccess.GameState`.

**Click-gate in automation**: nothing ticks (and `/screen`/`/eval` time out) until the
gate gets a click. Post one: find the EXAPUNKS window HWND, `PostMessage`
`WM_LBUTTONDOWN`(0x201) + `WM_LBUTTONUP`(0x202) with REAL client coordinates packed in
lParam (e.g. `(100 << 16) | 100`) — lParam 0, i.e. (0,0), does NOT release the gate.
(With the any-key splash advance in place, posting a `WM_KEYDOWN` with a real scancode
in lParam is the simpler route.)

**Synthetic keyboard caveat**: posted `WM_KEYDOWN` for SHIFT does not reach SDL's
modifier state (SDL normalizes shift against the thread keyboard state, which
PostMessage doesn't update) — so scripted Shift+chords (Shift+Tab) silently act
unshifted. Real keyboards are unaffected; test chorded bindings by hand.
Two more, learned the hard way: (1) keys posted while the game window is UNFOCUSED
drop intermittently (SDL discards key events without window focus — the terminal
running the script usually holds it), so `SetForegroundWindow` the game window before
posting key SEQUENCES (screenshots must still never do this — `/screenshot` works
occluded by design); (2) arrow keys need the EXTENDED-KEY bit in lParam
(`1 << 24`, e.g. Down = `(1) | (0x50 << 16) | (1 << 24)`) or SDL maps them to numpad
scancodes.

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
  `ControlTypes` (the role-word/type registry), and the NAVIGATOR layer ported from
  WrathAccess: `Navigator` (contract) / `Navigation` (static facade) /
  `GraphNavigator` (pull-based announce differ over the graph core: per-frame
  EnsureFocus, live-part watch, the ui.* input vocabulary; type-ahead and sound cues
  deliberately deferred). SELECTION-FOLLOWS-FOCUS: a node with `NodeVtable.OnSelect`
  runs it on every DIRECTIONAL landing (arrows/Home/End/region jumps — never Tab-stop
  cycling), before the announce; tabs and list rows select as you scroll, no Enter
  needed (Enter/OnActivate stays separate — task rows open on it). Such controls
  declare `NodeVtable.Selected` (engine-only — drives stop/entry landings) INSTEAD of
  a spoken Selected part: selection that always follows focus is never announced
  (user rules, 2026-08-22); radio options keep speaking theirs. TEXT ENTRY
  (`NodeVtable.TextEntry` + `TextValue`): a typing-first field — focus arriving ANY
  way (arrows, Tab-stop landing, programmatic) runs OnSelect to arm the game's field,
  printable keys flow via SDL TEXTINPUT (a channel suppression never touches), the
  navigator stands its Space/Backspace bindings down and passes keycode 8 through the
  suppression seam, and typed/DELETED characters echo bare — deletions speak just the
  character, single capitals speak "Cap X" (user rules, 2026-08-22), suppressible per
  node via `TextEchoCaps` for widgets that uppercase every insert. `FocusMode` —
  plain flag, ON by default; its game-side half is `Patches/GameKeySuppression.cs`.
- `module/src/Screens/` — `Screen` base + `ScreenManager` (WrathAccess poll-and-diff
  lifecycle; resolution = registered screens polling the GAME's screen stack via
  GameState). Unmodeled game screens still announce by friendly name (the retired
  ScreenAnnouncer's behavior, now the manager's fallback — obfuscated names logged,
  never spoken). `TitleScreen` — the first modeled screen: resolves its obfuscated
  game type BY SHAPE (the unique IScreen carrying bool(float,Texture,Vector2,
  LocString,LocString,*,Vector2,Bounds2)), two buttons (computer → DesktopScreen,
  tablet → ControlPanelScreen) activated via `Game/GameApi.PushScreen` —
  `GameLogic.method_12(IScreen)` (ordinal 12, signature cross-checked, unique-scan
  fallback), byte-identical to the game's own hotspot click.
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
  state reads, `SDL_GetKeyName`, + `SDL_PushEvent` (56-byte union — Size=64 on
  purpose): the splash's synthetic mouse click and `PushKey` (synthetic keyboard
  events). TWO PushKey traps, both hit and diagnosed live: the game's event loop
  ROUTES key events by the event's WINDOW ID through its private window map
  (GameLogic dictionary_0 — wrong/zero id = silently dropped; read the real id via
  Deobf, it is 1 in practice), and a key-DOWN and its key-UP processed in the same
  event pump cancel before the game's per-frame poll — hold the up back two frames.
- `module/src/Game/` — the thin typed game boundary (see "Typed game access"):
  `GameText` (the game's own localized strings — see Hard rules), `GameApi`
  (push/pop/quit wrappers over `GameLogic.method_12/15/32`), `Deobf` (private-member
  lookup via the namemap) + `Expr.MethodOf` (remappable Harmony targets), `SdlNative`
  (our own SDL P/Invokes for the splash/keyboard). Everything else touches the game
  directly in typed code at the call sites — the WrathAccess shape.
- `module/src/Screens/ControlPanelScreens.cs` — the control panel accessified: ONE game
  screen modeled as THREE mod screens keyed to its private page field (home / Options /
  Controls — page flips announce as screen changes, each keeps focus memory), tabs as a
  horizontal Tab-node row, labeled radio rows, volume sliders (±5%, percent feedback),
  the Redshift key bindings (activation defers the game's raw capture screen until all
  keys are up — it binds the first HELD key, which would otherwise be our Enter), the
  keyboard-reference table, and `GameKeyCaptureScreen` (CapturesRawInput, layer 10)
  over the game's capture overlay. Native mouse + Escape handling untouched throughout.
- `module/src/Screens/DesktopScreens.cs` — the desktop hub (`DesktopHubScreen`, see
  "Desktop decoded"): four Tab stops — organizer task list (rows ENUMERATED live from
  the campaign lists, the first place the WotR items-from-collections pattern applies;
  selection-follows-focus via OnSelect = the game's method_9, Enter opens via
  method_11), detail pane (title/date/description/location-or-hostname + the game's
  own action-button label), CHATSUBO (log + user roster as Ctrl+arrow regions; the
  Chat/Tasks tabs once unlocked), program launchers + close. KeepStateOnPop (desktop
  survives beneath cutscene/editor pushes). OnUpdate announces NEW chat lines and
  task appeared/completed events, watermarked PER GAME-DESKTOP INSTANCE — priming on
  focus would swallow the queued lines the game flushes on re-expose. Deferred:
  leaderboards/histograms, the multiplayer opponent table, side-jobs tab live-verify
  (needs an ember-7 save), the custom-win hold-button.
- `module/src/Screens/CutsceneScreens.cs` — `CutsceneScreen` over the visual-novel
  cutscene player (deob GClass255, obfuscated live — the nivas/ghast/isadora scenes;
  FULLY LINEAR: vignette script + current-line int, no choices): ANNOUNCE-ONLY with
  CapturesRawInput — the game's own Space/Tab/Enter/click advance and Escape skip stay
  live; OnUpdate watches the line index (privates via Deobf's T-row bridge) and speaks
  each line in full as it appears, speaker-prefixed per the drawn name plate (Moss =
  the player's plate-less narration, spoken bare; non-Moss lines are VOICE-ACTED and
  TTS currently reads them anyway, subtitle-style). `EmberCutsceneScreen` over the
  EMBER comic player (Ember2CutsceneScreen, name-preserved) — the game's only DIALOGUE
  CHOICES: each script line carries a LIST of texts (Ember2 lines = response variants
  picked by the player's previous choice int_2; non-Ember multi-text lines = answer
  bubbles, natively choosable by mouse or the undocumented number keys 1-9).
  Announce-only with CapturesRawInput EXCEPT while a choice is pending: then the
  options become a MENU — bare labels "1: text" (no role word, no position counts —
  user rule, 2026-08-22), arrows re-read, Enter picks by pushing the option's digit
  as a synthetic SDL key so the game's own choice path runs byte-identically; native
  digits keep working in parallel. Covers the fullscreen story mode. Plus
  `TrashWorldNewsScreen` — name-only over the zine reader (deob GClass214, obfuscated
  live; ghast-1/2 cutscenes end by pushing it): announces the game's own hotspot
  label, content reading is future work.
- `module/src/Patches/GameKeySuppression.cs` — the focus-mode key-suppression seam:
  Harmony prefixes on `GClass64.smethod_17/22` (via `Expr.MethodOf`; positional `__0`
  binding — shipping param names are obfuscated) return not-pressed for the navigator's
  keys (arrows + numpad, Enter/KP-Enter, Tab, Space, Backspace, Home/End/PgUp/PgDn)
  while FocusMode is on AND a modeled screen is focused AND it doesn't
  CapturesRawInput. Escape is deliberately NOT swallowed (native back/close paths
  stay). Unmodeled screens see every key — the game stays fully playable.
- `module/src/Screens/WorkhouseScreen.cs` — the Workhouse (deob GClass50, the one-time
  receipt-transcription minigame) — the first TEXT ENTRY screen, typing-first: field
  nodes are TextEntry (landing arms the game's own field via its method_3+method_2
  keyboard path; Enter hops to the next field; the game's append-only GClass60 widget
  needs no arrows, so navigation and typing never collide). Receipt = nine read-only
  rows mirroring the game's grading literal (the same lines the receipt art shows);
  SUBMIT/EXIT (mouse-only in the game) replicate the click paths exactly, including
  submit's normalize-then-Levenshtein grading via the game's own method_4; the
  art-only dialogs (exit confirm / rejected / success) become tiny button graphs with
  mirrored prompts, announced via flag watches; the fake-loading phase announces the
  game's own three loc strings. Falling-edge field release: when OUR focus leaves the
  fields, the game's field unfocuses like a click would (never fights a mouse user).
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
- **Game access is TYPED through the remap pipeline** (module side — see "Typed game
  access"): write plain C# against the deob names; never string-based reflection with
  deob names (it does not remap — `Deobf` for privates, `Expr.MethodOf` for patch
  targets). The HOST (which loads before any remapping exists) keeps the original
  discipline for its few members: types by real name, members by token-order ordinal,
  cross-checked by signature — never raw metadata tokens.
- **Never speak `#=q…`** — filter obfuscated names (`GameLogicPatches.IsObfuscated`)
  before anything reaches `Tts`; map screens to friendly labels as they're identified.
- **Localize every string the mod speaks** — `Loc.T(key[, args])` / `Message` + an
  entry in `module/assets/locale/enGB/ui.json` (the complete translation manifest;
  another language = a dropped-in folder under `<game>\ExaAccess\locale\`). Named
  `{placeholders}`, not string.Format. Exemptions: HOST emergency strings (spoken when
  the module itself failed to load — localization is module-side) and dev-only tooling.
- **Never duplicate a string the game already has.** Anything the game displays is read
  LIVE via `module/src/Game/GameText.cs` (the game's own loc registry, deob
  `GClass7.smethod_5(key, …) → LocString` — resolved by shape; six shipped languages,
  keys are literally the English text so failed lookups stay readable). `TSpeech`
  massages " / " separators/newlines for TTS. ui.json is ONLY for text the game
  genuinely lacks: role words, prompts for otherwise-silent screens, names for
  unlabeled/art-labeled things.
- **No mod-authored hints. Ever.** If the game doesn't provide hint/description text
  for a control, we don't invent it — sighted players get the art, blind players get
  the same label + role + value and nothing more (user rule, 2026-08-21). The Tooltip
  announcement channel exists only for tooltip text the game itself displays.
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
7. **(done)** Navigator glue: GraphNavigator + Screen/ScreenManager over the game's
   screen stack, input wired, TitleScreen modeled (keyboard-only from launch into the
   game, verified live).
8. **(done)** Control panel accessified end to end: home / Options (Display, Sound,
   Interface, Network) / Controls (Redshift bindings with the game's own key-capture
   flow, keyboard reference), all labels read live from the game's localization.
   Deferred there: hostname EDITING (needs the WotR TextEntry port; the value reads) —
   and note Exit Game quits instantly, faithful to the game's own button.
9. **(done)** Desktop hub modeled (`DesktopScreens.cs` — task list with
   selection-follows-focus, details + action button, CHATSUBO log/roster with live
   new-message and task-event announcements, launchers, close; verified live) AND the
   focus-mode key-suppression seam (`GameKeySuppression.cs` over the `GClass64` input
   facade — the game's own desktop keys no longer double-act; Escape stays native).
   Deferred on the desktop: leaderboards/histograms + the multiplayer opponent table
   (post-solve detail-pane content), the side-jobs tab live-verify (needs an ember-7
   save), the custom-win press-and-hold button.
10. **(partly done)** Desktop destinations: BOTH cutscene players are fully
    accessible (`CutsceneScreens.cs` — the visual-novel player speaks every line as
    it appears with native keys; the EMBER comic player additionally turns its
    dialogue choices into an arrow-navigable menu with Enter/digit selection,
    verified live through a real choice), the Workhouse is fully playable
    (`WorkhouseScreen.cs` — typing-first text entry, the seam the EXA editor will
    build on), and TRASH WORLD NEWS announces by name. Still to model: the EXA code
    editor (`EditorScreen` — the big one), the news reader's CONTENT, and
    `SolitaireScreen`/`ArcadeScreen`/`CustomPuzzleScreen` (friendly name only today).
11. Map the remaining obfuscated transition/overlay screens to friendly names.
12. Read the model: `Sim`/`SimExa`/`SimHost`/`Register`/`SimFile` for gameplay, the EXA
    code editor for program text — this game is text-centric, a strong a11y target.
13. Type-ahead search (WotR's TypeAheadSearch is pure — port with SDL TEXTINPUT), the
    settings tree, the mod menu, and the TextEntry port (unlocks hostname editing).
14. Installer (6 files + locale folder; uninstall = delete the config).

