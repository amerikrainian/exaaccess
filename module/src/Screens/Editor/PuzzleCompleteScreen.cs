using System;
using System.Reflection;
using ExaAccess.Game;
using ExaAccess.Localization;
using ExaAccess.UI;
using ExaAccess.UI.Graph;

namespace ExaAccess.Screens
{
    /// <summary>The puzzle-completion screen (name-preserved), BROWSABLE (user rule): the final
    /// scores as arrow-navigable rows (no position counts) plus the two native options as rows
    /// naming their keys. Enter and Escape stay on the game's own paths — Enter (Return to
    /// Desktop) rides the suppression pass-through, Escape (Continue Editing) is never
    /// suppressed. Scores: size from the screen's field; cycles/activity as the maxima of the
    /// editor's public per-run table.</summary>
    public sealed class PuzzleCompleteScreen : Screen
    {
        public override string Key => "puzzle.complete";
        public override string ScreenName => Loc.T("screen.PuzzleCompletionScreen");

        public override bool IsActive() => GameState.TopScreen() is PuzzleCompletionScreen;

        // Enter must reach the game's Return to Desktop even while our graph browses.
        public override bool PassKeyToGame(int keycode) => keycode == 13 || keycode == 1073741912;

        private static readonly FieldInfo SizeField = Deobf.Field(typeof(PuzzleCompletionScreen), "int_0");
        private static readonly FieldInfo EditorField = Deobf.Field(typeof(PuzzleCompletionScreen), "editorScreen_0");

        public override void Build(GraphBuilder b)
        {
            var s = GameState.TopScreen() as PuzzleCompletionScreen;
            if (s == null) return;
            b.PushContext(Loc.T("editor.stats"), positions: false);
            StatRow(b, "pc.cycles", () => ScoreManager.locString_0.ToString(), () => Stat(s, 0));
            StatRow(b, "pc.size", () => ScoreManager.locString_1.ToString(), () => Stat(s, 1));
            StatRow(b, "pc.activity", () => ScoreManager.locString_2.ToString(), () => Stat(s, 2));
            b.PopContext();
        }

        // The options are announced once on entry (user rule) — the browsable rows are the stats.
        public override void OnFocus()
        {
            base.OnFocus();
            try
            {
                Speech.Tts.Speak(Loc.T("editor.complete.options", new
                {
                    resume = GameText.T("Continue Editing"),
                    leave = GameText.T("Return to Desktop"),
                }));
            }
            catch { }
        }

        private static void StatRow(GraphBuilder b, string id, Func<string> label, Func<string> value)
        {
            b.AddItem(ControlId.Structural(id), new NodeVtable
            {
                ControlType = ControlTypes.Text,
                SpeaksOwnPosition = true, // no "n of m" anywhere on this screen (user rule)
                Announcements = new[]
                {
                    new NodeAnnouncement(label, kind: AnnouncementKinds.Label),
                    new NodeAnnouncement(value, kind: AnnouncementKinds.Value),
                },
            });
        }

        private static string Stat(PuzzleCompletionScreen s, int which)
        {
            try
            {
                if (which == 1) return SizeField != null ? SizeField.GetValue(s).ToString() : null;
                int best = 0;
                var editor = EditorField?.GetValue(s) as EditorScreen;
                if (editor != null)
                    foreach (var pair in editor.dictionary_0.Values)
                    {
                        int v = which == 0 ? pair.Item1 : pair.Item2;
                        if (v > best) best = v;
                    }
                return best.ToString();
            }
            catch { return null; }
        }
    }
}
