using System;
using System.IO;
using System.Reflection;
using HarmonyLib;

namespace ExaAccess
{
    /// <summary>
    /// The loader. EXAPUNKS is a single self-contained .NET Framework 4.x x64 executable running
    /// Zachtronics' own SDL2/D3D11 engine (NOT Unity), so we don't need Doorstop/BepInEx machinery to
    /// get managed code inside it — we simply BECOME the process: LoadFrom the game assembly, apply
    /// Harmony patches while no game code (not even a static ctor) has run yet, then invoke the game's
    /// entry point in-process. The patches are in place before frame one — the same guarantee BepInEx's
    /// chainloader gives on Unity, achieved here in three reflection calls.
    ///
    /// Install: this exe + 0Harmony.dll + prism.dll live IN the game folder; run this instead of the game
    /// (or set it as the Steam launch option, see README). Its working dir must be the game folder so the
    /// engine finds Content/ and the native dlls (SDL2, prism) resolve.
    /// </summary>
    public static class Loader
    {
        private const string GameExeName = "EXAPUNKS.exe";
        private const string SteamAppId = "716490";

        [STAThread]
        public static int Main(string[] args)
        {
            Log.Init();
            Log.Info("ExaAccess loader starting. Base dir: " + AppDomain.CurrentDomain.BaseDirectory);

            string gameExe;
            try { gameExe = ResolveGameExe(args); }
            catch (Exception ex)
            {
                Log.Error("Could not locate " + GameExeName, ex);
                return 2;
            }
            string gameDir = Path.GetDirectoryName(gameExe);
            Log.Info("Game exe: " + gameExe);

            // The engine looks for Content/ relative to the working directory (GameLogic.method_8), and
            // native deps (SDL2.dll, prism.dll) resolve from it. Pin cwd to the game folder.
            try { Directory.SetCurrentDirectory(gameDir); }
            catch (Exception ex) { Log.Error("Failed to set working directory to " + gameDir, ex); }

            EnsureSteamAppId(gameDir);

            // Speech first, so we can announce even if patching later fails.
            if (Speech.Tts.Init())
                Speech.Tts.Speak("Exa Access starting.");
            else
                Log.Warning("Speech unavailable — continuing so the game still launches.");

            Assembly game;
            try
            {
                game = Assembly.LoadFrom(gameExe);
                Log.Info("Loaded game assembly: " + game.FullName);
            }
            catch (Exception ex)
            {
                Log.Error("Failed to load the game assembly", ex);
                return 3;
            }

            GameState.Bind(game);
            if (!ApplyPatches(game))
                Log.Warning("Harmony patches failed to attach — the game will still run, just without accessibility hooks.");

#if DEBUG
            try { Dev.DevServer.Instance.Start(); }
            catch (Exception ex) { Log.Error("Dev server failed to start", ex); }
#endif

            // Hand control to the game. Its entry point runs the while(true) render loop and does not
            // return until the game exits, so this call blocks for the whole session.
            return InvokeGame(game);
        }

        /// <summary>Find EXAPUNKS.exe. Normally the loader is deployed INTO the game folder, so it sits
        /// right next to it. When launched via Steam's "%command%" the game exe path also arrives in
        /// args — accept that as a fallback.</summary>
        private static string ResolveGameExe(string[] args)
        {
            string beside = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, GameExeName);
            if (File.Exists(beside)) return Path.GetFullPath(beside);

            foreach (var a in args)
            {
                if (a != null && a.EndsWith(GameExeName, StringComparison.OrdinalIgnoreCase) && File.Exists(a))
                    return Path.GetFullPath(a);
            }
            throw new FileNotFoundException(
                GameExeName + " was not found next to the loader or in its arguments. " +
                "Deploy ExaAccess.exe into the EXAPUNKS install folder.");
        }

        /// <summary>Write steam_appid.txt next to the game so a direct launch is recognized by
        /// Steamworks. Without it, GameLogic.method_8()'s SteamAPI.RestartAppIfNecessary(716490) returns
        /// true and our hosted process exits while Steam relaunches the vanilla exe — losing our patches.
        /// (When launched via the Steam "%command%" option Steam sets this up itself; writing it is a
        /// harmless, idempotent belt-and-suspenders.)</summary>
        private static void EnsureSteamAppId(string gameDir)
        {
            string path = Path.Combine(gameDir, "steam_appid.txt");
            try
            {
                if (!File.Exists(path) || File.ReadAllText(path).Trim() != SteamAppId)
                {
                    File.WriteAllText(path, SteamAppId);
                    Log.Info("Wrote steam_appid.txt (" + SteamAppId + ").");
                }
            }
            catch (Exception ex)
            {
                // If the folder isn't writable, the Steam "%command%" launch path still works.
                Log.Warning("Could not write steam_appid.txt (" + ex.Message + "). " +
                    "If the game bounces back to the vanilla exe, launch via the Steam launch-option method instead.");
            }
        }

        private static bool ApplyPatches(Assembly game)
        {
            try
            {
                // GameState resolved these by token-order ordinal (the shipping exe's member names are
                // obfuscated; see GameState for why ordinals, not names, are the stable handle).
                var init = GameState.InitMethod;
                var tick = GameState.TickMethod;
                if (init == null || tick == null)
                {
                    Log.Error("[patch] init/tick methods unresolved (init=" + (init != null) + ", tick=" + (tick != null) + ").");
                    return false;
                }

                var harmony = new Harmony("com.exaaccess.loader");
                var patches = typeof(Patches.GameLogicPatches);
                harmony.Patch(init, postfix: new HarmonyMethod(patches.GetMethod(nameof(Patches.GameLogicPatches.AfterInit))));
                harmony.Patch(tick, prefix: new HarmonyMethod(patches.GetMethod(nameof(Patches.GameLogicPatches.BeforeTick))));
                Log.Info("[patch] attached postfix to init and prefix to tick.");
                return true;
            }
            catch (Exception ex)
            {
                Log.Error("[patch] failed to attach Harmony patches", ex);
                return false;
            }
        }

        private static int InvokeGame(Assembly game)
        {
            var entry = game.EntryPoint;
            if (entry == null) { Log.Error("Game assembly has no entry point."); return 4; }
            Log.Info("Invoking game entry point: " + entry.DeclaringType.FullName + "::" + entry.Name);
            try
            {
                // The entry point is `static void Main(string[])`; hand it an empty argv (a normal launch
                // needs none — it only checks for "-bake" and a single-instance mutex).
                entry.Invoke(null, new object[] { new string[0] });
                return 0;
            }
            catch (TargetInvocationException tie)
            {
                Log.Error("Game threw during execution", tie.InnerException ?? tie);
                return 1;
            }
            catch (Exception ex)
            {
                Log.Error("Failed to invoke the game entry point", ex);
                return 1;
            }
            finally
            {
                Speech.Tts.Shutdown();
            }
        }
    }
}
