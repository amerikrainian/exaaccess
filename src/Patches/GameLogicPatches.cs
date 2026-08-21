using System;

namespace ExaAccess.Patches
{
    /// <summary>
    /// The mod's Harmony patches on <c>GameLogic</c> — deliberately thin: they only translate the
    /// game's lifecycle into mod-side events, all real behavior lives in the subsystems.
    ///
    ///   • <see cref="AfterInit"/>  — postfix on GameLogic.method_8() (one-time init: SDL/window/textures/
    ///     Steam are all up). Announces the mod is live at the moment the window appears.
    ///   • <see cref="BeforeTick"/> — prefix on GameLogic.method_25() (the per-frame tick from Main's
    ///     while(true) loop). Runs <see cref="FrameLoop"/> — the registry every per-frame subsystem
    ///     (dev pump, screen watcher, input, UI) hangs off.
    ///
    /// These are attached MANUALLY from <see cref="Bootstrap"/> (harmony.Patch with reflected MethodInfos),
    /// not via [HarmonyPatch] attributes, because we don't reference the obfuscated game assembly at
    /// compile time — there is no <c>typeof(GameLogic)</c> to name. Instance methods receive the live
    /// object as <c>object __instance</c> (Harmony's magic parameter), typed as object since we can't
    /// name the game type. Every hook body is defensive: an accessibility mod must never crash the game.
    /// </summary>
    public static class GameLogicPatches
    {
        private static bool _announcedReady;

        /// <summary>Postfix on GameLogic.method_8() — init finished.</summary>
        public static void AfterInit(object __instance)
        {
            try
            {
                if (_announcedReady) return; // method_8 runs once, but guard anyway
                _announcedReady = true;
                Log.Info("[hook] GameLogic init complete — ExaAccess is live.");
                Speech.Tts.Speak("ExaAccess ready.");
                // Forget any pre-init screen state so the first tick announces the opening screen.
                UI.ScreenAnnouncer.Instance.Reset();
            }
            catch (Exception ex) { Log.Error("[hook] AfterInit failed", ex); }
        }

        /// <summary>Prefix on GameLogic.method_25() — runs at the top of every frame.</summary>
        public static void BeforeTick(object __instance)
        {
            try { FrameLoop.Tick(); } // FrameLoop catches per-step; this guards the loop itself.
            catch (Exception ex) { Log.Error("[hook] BeforeTick failed", ex); }
        }
    }
}
