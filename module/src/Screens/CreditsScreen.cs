using System;
using System.Collections.Generic;
using System.Reflection;
using Echopunks.Game;
using Echopunks.Localization;

namespace Echopunks.Screens
{
    /// <summary>
    /// The game's VICTORY screen: the credits roll (deob GClass252, obfuscated live), pushed
    /// when the final EMBER-2 story epilogue ends. ~73 seconds, fully timed and
    /// non-interactive — black screen, music, one card after another (logo, the five team
    /// cards with portraits, the staggered voice cast, thanks, the limited-edition envelope
    /// prompt) — then it marks CreditsSeen and transitions out by itself. Escape skips it
    /// only once CreditsSeen is set (the game's own rule, left native).
    /// ANNOUNCE-ONLY with CapturesRawInput, like the cutscene players: every line is real
    /// drawn text, read live through PanelCapture's credits tap (no string duplicated);
    /// OnUpdate speaks each line the frame it first appears — the staggered cast block
    /// accumulates on screen, so only the ARRIVALS speak. The opening logo card is a sprite:
    /// its lettering is the game's title, transcribed (language-invariant art, the
    /// NetworkLogos pattern). Portraits are art.
    /// </summary>
    public sealed class CreditsScreen : Screen
    {
        public override string Key => "credits";
        public override string ScreenName => Loc.T("screen.credits");
        public override bool IsActive() => GameState.TopScreen() is GClass252;
        public override bool CapturesRawInput => true;

        // float_0 = the roll's clock; the logo card shows from 3.35s to 6.35s (the draw's literals).
        private static readonly FieldInfo ClockField = Deobf.Field(typeof(GClass252), "float_0");
        private const float LogoFrom = 3.35f, LogoTo = 6.35f;
        private const string LogoLettering = "EXAPUNKS";

        private HashSet<string> _shown = new HashSet<string>(StringComparer.Ordinal);
        private bool _logoSpoken;

        public override void OnPush()
        {
            _shown.Clear();
            _logoSpoken = false;
        }

        public override void OnUpdate()
        {
            try
            {
                var roll = GameState.TopScreen() as GClass252;
                if (roll == null) return;
                if (!_logoSpoken && ClockField != null)
                {
                    float t = (float)ClockField.GetValue(roll);
                    if (t >= LogoFrom && t <= LogoTo)
                    {
                        _logoSpoken = true;
                        Speech.Tts.Speak(LogoLettering);
                    }
                }
                var frame = Patches.PanelCapture.CreditsFrame;
                var now = new HashSet<string>(StringComparer.Ordinal);
                var arrivals = new List<string>();
                foreach (var line in frame)
                {
                    if (!now.Add(line)) continue;
                    if (!_shown.Contains(line)) arrivals.Add(GameText.Speech(line));
                }
                _shown = now; // a line that left the screen may return on a later card (repeated roles)
                if (arrivals.Count > 0) Speech.Tts.Speak(string.Join(", ", arrivals));
            }
            catch (Exception ex) { Log.Error("[credits] update failed", ex); }
        }
    }
}
