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
patching library you actually want — **Harmony** — we use directly, and the CLR itself provides the
injection point: an **`EXAPUNKS.exe.config`** dropped next to the game names `ExaAccess.Bootstrap`
(`src/Bootstrap.cs`) as the process's **AppDomainManager**, which the runtime instantiates inside the
**stock** `EXAPUNKS.exe` before the game's entry point — or any of its static ctors — runs:

```xml
<runtime>
  <appDomainManagerAssembly value="ExaAccess, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null" />
  <appDomainManagerType value="ExaAccess.Bootstrap" />
</runtime>
```

That gives the same "patched before frame one" guarantee BepInEx's chainloader gives on Unity, from one
config file. Steam's Play button just works; no game file is modified, so "verify integrity" leaves the
install alone, and deleting the config restores a fully vanilla launch. (The mod originally shipped as a
loader exe that `Assembly.LoadFrom`'d the game and invoked its entry point in-process — same guarantee,
but users had to launch the loader instead of the game. The config route made it redundant.)

## Architecture

```
src/
  Bootstrap.cs           The mod's entry point: AppDomainManager the CLR instantiates inside the
                         STOCK EXAPUNKS.exe (via deploy/EXAPUNKS.exe.config) before any game code
                         runs — set cwd, steam_appid, boot speech, bind members, Harmony patch.
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
deploy/
  EXAPUNKS.exe.config    The zero-loader hookup (appDomainManagerAssembly/Type → ExaAccess.Bootstrap).
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
`ExaAccess.dll`, `0Harmony.dll`, `prism.dll`, `EXAPUNKS.exe.config` (and `Mono.CSharp.dll` for the dev
REPL), and writes `steam_appid.txt`. `dotnet build -c Release` compiles the shipping build with no dev
server.

## Running it

With the Steam client running, just start **`EXAPUNKS.exe`** — Steam Play button, shortcut, anything.
The deployed `EXAPUNKS.exe.config` makes the CLR load `ExaAccess.Bootstrap` inside the stock process
before the game runs. This is all the future installer sets up: copy `EXAPUNKS.exe.config`,
`ExaAccess.dll`, `0Harmony.dll`, `prism.dll`, `steam_appid.txt` into the game folder — done.

Two things to know:

- The config binds the mod assembly by **full display name**; `AssemblyVersion` is pinned at 1.0.0.0 in
  the csproj so they can't drift apart.
- `steam_appid.txt` (written by the build, and by Bootstrap as a fallback) stops `GameLogic` init's
  `SteamAPI.RestartAppIfNecessary(716490)` from restarting a non-Steam launch through Steam. Even if
  that bounce happens, the relaunched stock exe re-enters through the same config and still loads the
  mod — it's cosmetic, not fatal.

To play **vanilla**, delete `EXAPUNKS.exe.config` from the game folder; no game file is ever modified,
so Steam's "verify integrity" is never needed to undo the mod.

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

- **Zero-loader launch**: starting the stock `EXAPUNKS.exe` boots the mod via the AppDomainManager
  config — verified end-to-end (patches fire, speech speaks, dev server drives the live game).
- Harmony patches attach to obfuscated `GameLogic` methods (resolved by ordinal): init postfix and the
  per-frame tick prefix both fire.
- Speech works: "ExaAccess ready" spoken through NVDA at boot.
- Dev server drives/observes the live game, including the C# REPL.

## Near-term next steps

- First screen over the ported UI graph (navigator + screen stack glue).
- Map the obfuscated-name transition/overlay screens (major screens already have real names).
- Read the **model**, not pixels: `Sim`/`SimExa`/`SimHost`/`Register`/`SimFile` for gameplay state, and
  the code editor for EXA program text — this game is unusually text-centric and a strong a11y target.
- Port the richer config-driven speech stack (SAPI/positional/settings) behind the existing `Tts` facade.
- Shipping distribution: keep resolving the retail exe by ordinal (never redistribute the deob copy).
