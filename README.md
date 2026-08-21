# ExaAccess — screen-reader accessibility mod for EXAPUNKS

A proof-of-concept accessibility layer for the Zachtronics game **EXAPUNKS**, aimed at blind and
visually-impaired players. It speaks game state through the player's screen reader (NVDA/JAWS/SAPI/
OneCore, via [Prism](https://github.com/ethindp/prism)) and is built to grow into a full model-driven
reader of the desktop, code editor, and simulation.

Status: **POC proven end-to-end** — the game loads under our loader, Harmony patches attach to the
(obfuscated) engine, and the mod speaks. See "What works today" below.

---

## Why this isn't a BepInEx / Unity mod

EXAPUNKS looks like it uses SDL2, which suggests Unity — it does **not**. `EXAPUNKS.exe` (internal name
`Burbank`) is a single self-contained **.NET Framework 4.x, x64** assembly running Zachtronics' own
SDL2 + Direct3D11 engine. SDL2/`Renderer_D3D11.dll` are called from C# via P/Invoke.

Because it's an ordinary managed exe, there is no native Unity player to bootstrap into, so the hard
problem BepInEx exists to solve (Doorstop/IL2CPP interop/domain timing) simply doesn't apply. The
patching library you actually want — **Harmony** — we use directly. Our loader *becomes* the process:

```
Assembly game = Assembly.LoadFrom("EXAPUNKS.exe");
GameState.Bind(game);              // resolve engine members (see "Obfuscation")
new Harmony("...").Patch(...);     // patches in place before ANY game code runs
game.EntryPoint.Invoke(null, ...); // hand control to the game
```

That gives the same "patched before frame one" guarantee BepInEx's chainloader gives on Unity, in three
reflection calls — and a dead-simple install for users (drop files in the folder, run one exe).

## Architecture

```
src/
  Loader.cs              Entry point: locate game, set cwd, steam_appid, boot speech,
                         LoadFrom + Harmony patch + invoke the game's entry point.
  Log.cs                 File logger → %LOCALAPPDATA%\ExaAccess\exaaccess.log (+ console).
  GameState.cs           Reflection-cached view of the live game; resolves engine members
                         by token-ordinal (see below). The seam every model read goes through.
  Speech/
    PrismNative.cs       P/Invoke over prism.dll (screen-reader / TTS abstraction).
    Tts.cs               Thin speech facade (best-available Prism backend; Speak/Stop).
  Patches/
    GameLogicPatches.cs  Harmony hooks: postfix on init (announce ready), prefix on the
                         per-frame tick (main-thread pump + screen-change announcements).
  Dev/                   DEBUG-only loopback HTTP test server (see "Dev server").
third_party/
  prism/                 prism.dll + header/license (screen-reader bridge). [committed]
  harmony/               0Harmony.dll 2.4.2 net48 (MIT). [committed]
  monocsharp/            Mono.CSharp.dll — powers the DEBUG /eval REPL. [committed]
game/                    Deobfuscated + decompiled game for ANALYSIS ONLY. [gitignored — never shipped]
```

## Obfuscation: how we target the engine

EXAPUNKS is obfuscated with **Eazfuscator.NET**. We run **de4dot** on a local copy to produce a clean,
string-decrypted assembly (`game/EXAPUNKS-deob.exe`) and decompile it (`game/decompiled/`) for reading.
That tree is a local analysis workspace — **gitignored, never distributed**.

Key facts that shape the code:

- The **shipping** exe keeps **real type names** for most types (`GameLogic`, `IScreen`, `DesktopScreen`,
  `Renderer`, `Sim`, …) but its **members** are still `#=q…` gibberish. de4dot's readable member names
  (`method_8`, `class5_0`) exist only in our deob copy.
- de4dot does **not** preserve metadata tokens (it strips obfuscator junk, shifting them) — but it
  **does** preserve each type's member **order**. So de4dot's `method_N` is exactly the N-th method of
  that type in `MetadataToken` order, and the same ordinal selects the same member on the shipping exe.

So `GameState` resolves **types by real name** and **members by token-order ordinal**, cross-checking
each by signature (and pinning fields by their unique type where possible) so a future game update fails
loudly instead of silently patching the wrong method. The ordinals in use:

| Member | Ordinal | Role |
|---|---|---|
| `GameLogic` method[8]  | init  | one-time setup (window/textures/Steam), then returns |
| `GameLogic` method[25] | tick  | the per-frame loop body (`while(true)` in `Main`) |
| `GameLogic` field[0]   | `gameLogic_0` | static self-reference to the live instance |
| `GameLogic` field[21]  | `class5_0`    | `Class5<IScreen>` screen stack; top = last element |

## Build & deploy

Requires the .NET SDK and the .NET Framework 4.8 targeting pack.

```
dotnet build ExaAccess.csproj -c Debug
```

A **Debug** build deploys into the game folder (override with `-p:GameDir="..."`): it copies
`ExaAccess.exe`, `0Harmony.dll`, `prism.dll` (and `Mono.CSharp.dll` for the dev REPL), and writes
`steam_appid.txt`. `dotnet build -c Release` compiles the shipping loader with no dev server.

## Running it

The Steam client must be running. Then either:

1. **Direct:** run `ExaAccess.exe` from inside the game folder. `steam_appid.txt` (written by the build,
   and by the loader as a fallback) makes Steamworks accept the launch instead of bouncing to the vanilla
   exe (`GameLogic` init calls `SteamAPI.RestartAppIfNecessary(716490)`).
2. **Via Steam:** set the game's launch options to `"…\ExaAccess.exe" %command%` so Steam sets up its
   environment and hands us the game exe path.

**Boot note:** EXAPUNKS' loading screen waits for a **mouse click** (SDL `MOUSEBUTTONDOWN`) before `init`
returns and the game reaches its first screen — this is the game's own behavior. A blind player needs a
click there today; auto-advance/announcement is a near-term to-do.

## Dev server (DEBUG only)

Gated behind `EXAACCESS_DEV=1` (or a `devserver.enable` marker file in the game folder). A loopback HTTP
server on `127.0.0.1:8772` for driving/observing the live game — the test harness ported from the
WrathAccess architecture, trimmed to the POC:

| Route | Purpose |
|---|---|
| `GET /health` | liveness |
| `POST /say` | speak the request body through the real speech path |
| `GET /speech?since=N` | lines the mod has spoken since cursor N (we can't hear the TTS) |
| `GET /screen` | active screen + full screen stack, by type name |
| `POST /eval` | run C# against the live game on the main thread (persistent REPL) |

`/eval` and `/screen` run on the game's main thread (queued and pumped from the tick prefix); `/say` and
`/speech` answer directly. Example:

```
curl -s -X POST --data 'foreach (var n in ExaAccess.GameState.ScreenStackNames()) Console.WriteLine(n);' \
     http://127.0.0.1:8772/eval
```

## What works today (proven)

- Loader injects into the obfuscated game in-process; `steam_appid.txt` prevents the relaunch bounce.
- Harmony patches attach to obfuscated `GameLogic` methods (resolved by ordinal): init postfix and the
  per-frame tick prefix both fire.
- Speech works: "ExaAccess ready" spoken through NVDA at boot.
- Dev server drives/observes the live game, including the C# REPL.

## Near-term next steps

- Handle the boot click-gate (auto-advance or announce).
- Map the obfuscated-name transition/overlay screens (major screens already have real names).
- Read the **model**, not pixels: `Sim`/`SimExa`/`SimHost`/`Register`/`SimFile` for gameplay state, and
  the code editor for EXA program text — this game is unusually text-centric and a strong a11y target.
- Port the richer config-driven speech stack (SAPI/positional/settings) behind the existing `Tts` facade.
- Shipping distribution: keep resolving the retail exe by ordinal (never redistribute the deob copy).
```
