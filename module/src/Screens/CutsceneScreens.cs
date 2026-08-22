using System.Reflection;
using ExaAccess.Game;
using ExaAccess.Localization;

namespace ExaAccess.Screens
{
    /// <summary>
    /// The visual-novel cutscene player (deob GClass255 — the nivas/ghast/isadora scenes; the
    /// desktop pushes it for cutscene tasks). FULLY LINEAR: `vignette_0.list_0` is the script,
    /// `int_0` the current line, no choices anywhere. The game's own keys do everything —
    /// Space/Tab/Enter/click advance (first press completes the typewriter, second moves on),
    /// Escape skips the scene — so this screen is ANNOUNCE-ONLY with CapturesRawInput: it watches
    /// the line index and speaks each line as it appears (full text immediately; the typewriter is
    /// visual pacing). Speaker prefix mirrors the drawn name plate (the character enum name);
    /// Moss lines are the player's silent narration and draw no plate, so none is spoken. Note:
    /// non-Moss lines are VOICE-ACTED — TTS currently reads them anyway (subtitle behavior).
    /// </summary>
    public sealed class CutsceneScreen : Screen
    {
        public override string Key => "cutscene";
        public override string ScreenName => Loc.T("screen.cutscene");
        public override bool CapturesRawInput => true; // the game's advance/skip keys must see everything

        public override bool IsActive() => GameState.TopScreen() is GClass255;

        // GClass255's name is obfuscated live — Deobf bridges via the namemap's T rows.
        private static readonly FieldInfo LineIndexField = Deobf.Field(typeof(GClass255), "int_0");
        private static readonly FieldInfo VignetteField = Deobf.Field(typeof(GClass255), "vignette_0");

        private object _lastInstance;
        private int _lastLine = -1;

        public override void OnUpdate()
        {
            var scene = GameState.TopScreen() as GClass255;
            if (scene == null || LineIndexField == null || VignetteField == null) return;

            int line;
            Vignette vignette;
            try
            {
                line = (int)LineIndexField.GetValue(scene);
                vignette = VignetteField.GetValue(scene) as Vignette;
            }
            catch { return; }
            if (vignette == null || line < 0 || line >= vignette.list_0.Count) return;

            if (ReferenceEquals(scene, _lastInstance) && line == _lastLine) return;
            _lastInstance = scene;
            _lastLine = line;

            var entry = vignette.list_0[line];
            string text = null;
            try { text = GameText.Speech(entry.list_0[0].ToString().Trim()); }
            catch { }
            if (string.IsNullOrEmpty(text)) return;

            // Moss (the player) narrates plate-less; everyone else gets the drawn name plate.
            string spoken = entry.vignetteCharacter_0 == VignetteCharacter.Moss
                ? text
                : Loc.T("cutscene.line", new { name = entry.vignetteCharacter_0.ToString(), text });
            Speech.Tts.Speak(spoken);
        }
    }

    /// <summary>The TRASH WORLD NEWS zine reader (deob GClass214 — obfuscated live, so the
    /// unmodeled-screen fallback cannot name it). Name-only for now: announce it with the game's
    /// own hotspot label and stand aside; reading the zine content is future work.</summary>
    public sealed class TrashWorldNewsScreen : Screen
    {
        public override string Key => "news";
        public override string ScreenName => GameText.TSpeech("TRASH\nWORLD") + " " + GameText.T("NEWS");
        public override bool CapturesRawInput => true; // the game's own reader keys stay live

        public override bool IsActive() => GameState.TopScreen() is GClass214;
    }
}
