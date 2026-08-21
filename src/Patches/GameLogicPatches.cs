using System;

namespace ExaAccess.Patches
{
    /// <summary>
    /// The mod's Harmony patches on <c>GameLogic</c> — proof that we can patch obfuscated game methods,
    /// and the two hooks everything else will hang off:
    ///
    ///   • <see cref="AfterInit"/>  — postfix on GameLogic.method_8() (one-time init: SDL/window/textures/
    ///     Steam are all up). Announces the mod is live at the moment the window appears.
    ///   • <see cref="BeforeTick"/> — prefix on GameLogic.method_25() (the per-frame tick from Main's
    ///     while(true) loop). Our main-thread heartbeat: pumps the dev server and announces screen changes.
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
        private static string _lastScreen;

        /// <summary>Postfix on GameLogic.method_8() — init finished.</summary>
        public static void AfterInit(object __instance)
        {
            try
            {
                if (_announcedReady) return; // method_8 runs once, but guard anyway
                _announcedReady = true;
                Log.Info("[hook] GameLogic init complete — ExaAccess is live.");
                Speech.Tts.Speak("ExaAccess ready.");
                // Prime the screen tracker so the first BeforeTick announces the opening screen.
                _lastScreen = null;
            }
            catch (Exception ex) { Log.Error("[hook] AfterInit failed", ex); }
        }

        /// <summary>Prefix on GameLogic.method_25() — runs at the top of every frame.</summary>
        public static void BeforeTick(object __instance)
        {
#if DEBUG
            // Run queued dev-server jobs (/eval, /screen) on the game's own thread this frame.
            try { Dev.DevServer.Instance.Pump(); } catch (Exception ex) { Log.Error("[hook] dev pump failed", ex); }
#endif
            try
            {
                string name = GameState.TopScreenName();
                if (name != null && name != _lastScreen)
                {
                    _lastScreen = name;
                    Log.Info("[hook] screen -> " + name);
                    // The major screens keep real names; a few transient overlay/transition screens are
                    // still obfuscated (#=q…). Don't read gibberish — skip those until they're mapped.
                    if (!IsObfuscated(name))
                        Speech.Tts.Speak(Friendly(name));
                }
            }
            catch (Exception ex) { Log.Error("[hook] BeforeTick screen poll failed", ex); }
        }

        // Turn a screen's obfuscation-recovered type name into something speakable. This is a first pass:
        // known screens get a friendly label; anything else is de-CamelCased so it's at least intelligible.
        private static string Friendly(string typeName)
        {
            switch (typeName)
            {
                case "DesktopScreen": return "Desktop";
                case "EditorScreen": return "Editor";
                case "ControlPanelScreen": return "Control panel";
                case "PuzzleCompletionScreen": return "Puzzle complete";
                case "BattleCompletionScreen": return "Battle complete";
                case "SolitaireScreen": return "Solitaire";
                case "OpponentBrowserScreen": return "Opponent browser";
                case "CustomPuzzleScreen": return "Custom puzzle";
                case "GifRecorderScreen": return "GIF recorder";
                default:
                    string s = typeName;
                    if (s.EndsWith("Screen", StringComparison.Ordinal)) s = s.Substring(0, s.Length - "Screen".Length);
                    return DeCamel(s);
            }
        }

        // de4dot leaves Eazfuscator's unprintable/`#=q…` names on types it couldn't rename semantically.
        private static bool IsObfuscated(string name)
        {
            if (string.IsNullOrEmpty(name)) return true;
            if (name.StartsWith("#=q", StringComparison.Ordinal)) return true;
            foreach (char c in name)
                if (!char.IsLetterOrDigit(c) && c != '_') return true;
            return false;
        }

        private static string DeCamel(string s)
        {
            if (string.IsNullOrEmpty(s)) return s;
            var sb = new System.Text.StringBuilder(s.Length + 8);
            for (int i = 0; i < s.Length; i++)
            {
                char c = s[i];
                if (i > 0 && char.IsUpper(c) && !char.IsUpper(s[i - 1])) sb.Append(' ');
                sb.Append(c);
            }
            return sb.ToString();
        }
    }
}
