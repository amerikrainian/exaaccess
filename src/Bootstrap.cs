using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using HarmonyLib;

namespace ExaAccess
{
    /// <summary>
    /// The mod's only entry point. EXAPUNKS is a single self-contained .NET Framework 4.x x64
    /// executable running Zachtronics' own SDL2/D3D11 engine (NOT Unity), so no Doorstop/BepInEx
    /// machinery is needed to get managed code inside it: an EXAPUNKS.exe.config dropped next to the
    /// game (see deploy/EXAPUNKS.exe.config) names this class as the process's AppDomainManager, and
    /// the CLR instantiates it in the default AppDomain BEFORE the game's entry point — or any game
    /// static ctor — runs. Patches are in place before frame one, inside the stock process: the Steam
    /// Play button, shortcuts, and controller launches all Just Work.
    ///
    /// The game's own files are never modified, so Steam's "verify integrity" leaves the install alone
    /// (it ignores extra files); deleting the config restores a fully vanilla launch.
    ///
    /// Install = copy into the game folder: EXAPUNKS.exe.config, ExaAccess.dll, 0Harmony.dll,
    /// prism.dll, steam_appid.txt. The config binds this assembly by FULL display name, so
    /// AssemblyVersion is pinned in the csproj — bump both in lockstep or the mod silently stops
    /// loading.
    /// </summary>
    public sealed class Bootstrap : AppDomainManager
    {
        private const string SteamAppId = "716490";

        public override void InitializeNewDomain(AppDomainSetup appDomainInfo)
        {
            base.InitializeNewDomain(appDomainInfo);
            // Secondary AppDomains (should the game ever create one) get their own manager instance;
            // only the default domain hosts the mod.
            if (!AppDomain.CurrentDomain.IsDefaultAppDomain()) return;
            try { Boot(); }
            catch (Exception ex)
            {
                // An exception escaping InitializeNewDomain kills the process before the game starts.
                // Whatever went wrong, the user must still get their vanilla game.
                try { Log.Error("Bootstrap failed — game continues without accessibility hooks.", ex); }
                catch { }
            }
        }

        private static void Boot()
        {
            Log.Init();
            Log.Info("ExaAccess bootstrap (AppDomainManager inside the stock exe) starting.");

            // BaseDirectory is the game folder (this config-driven path only exists there). Pin the
            // working directory to it: the engine resolves Content/ and the native dlls relative to
            // cwd, which a console launch from elsewhere would otherwise break.
            string gameDir = AppDomain.CurrentDomain.BaseDirectory;
            try { Directory.SetCurrentDirectory(gameDir); }
            catch (Exception ex) { Log.Error("Failed to set working directory to " + gameDir, ex); }

            EnsureSteamAppId(gameDir);

            // The game assembly is this very process's exe. GetEntryAssembly() can still be null this
            // early in CLR startup; LoadFrom on the process image path yields the same assembly
            // identity the entry point will execute from, so patches land on the live instance.
            Assembly game = Assembly.GetEntryAssembly();
            if (game == null)
                game = Assembly.LoadFrom(Process.GetCurrentProcess().MainModule.FileName);
            Log.Info("Game assembly: " + game.FullName);

            // Speech first, so we can announce even if patching later fails.
            if (Speech.Tts.Init())
                Speech.Tts.Speak("Exa Access starting.");
            else
                Log.Warning("Speech unavailable — continuing so the game still launches.");

            GameState.Bind(game);
            if (!ApplyPatches(game))
                Log.Warning("Harmony patches failed to attach — the game will still run, just without accessibility hooks.");

#if DEBUG
            try { Dev.DevServer.Instance.Start(); }
            catch (Exception ex) { Log.Error("Dev server failed to start", ex); }
#endif
        }

        /// <summary>Write steam_appid.txt next to the game so Steamworks accepts a launch that didn't
        /// come from Steam. Without it, GameLogic's init calls SteamAPI.RestartAppIfNecessary(716490),
        /// which returns true and exits this process while Steam starts a fresh one — a cosmetic double
        /// launch rather than a mod failure, since the relaunched stock exe re-enters through the same
        /// config, but skip the churn when we can.</summary>
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
                Log.Warning("Could not write steam_appid.txt (" + ex.Message + "). " +
                    "A Steam-relaunch bounce may occur; the relaunched game still loads the mod.");
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

                var harmony = new Harmony("com.exaaccess");
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
    }
}
