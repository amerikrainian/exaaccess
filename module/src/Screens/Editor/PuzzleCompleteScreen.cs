using System;
using System.Reflection;
using ExaAccess.Game;
using ExaAccess.Localization;
using ExaAccess.UI;
using ExaAccess.UI.Graph;

namespace ExaAccess.Screens
{
    /// <summary>The puzzle-completion screen (name-preserved), BROWSABLE (user rule): the final
    /// scores as arrow-navigable rows (no position counts), the Leaderboards/Test Run Data
    /// flip, the Record Solution GIF button, and the leave button under the game's own label
    /// (Return to Desktop / Return to VirtualNetwork+). Enter activates the FOCUSED control —
    /// it no longer rides the suppression pass-through to the game's leave shortcut, which
    /// made the flip/GIF nodes unactivatable (user request, 2026-08-22); Escape (Continue
    /// Editing) stays on the game's own never-suppressed path. Scores: size from the screen's
    /// field; cycles/activity as the maxima of the editor's public per-run table.</summary>
    public sealed partial class PuzzleCompleteScreen : Screen
    {
        public override string Key => "puzzle.complete";
        public override string ScreenName => Loc.T("screen.PuzzleCompletionScreen");

        public override bool IsActive() => GameState.TopScreen() is PuzzleCompletionScreen;

        private static readonly FieldInfo SizeField = Deobf.Field(typeof(PuzzleCompletionScreen), "int_0");
        private static readonly FieldInfo EditorField = Deobf.Field(typeof(PuzzleCompletionScreen), "editorScreen_0");
        private static readonly FieldInfo TabField = Deobf.Field(typeof(PuzzleCompletionScreen), "bool_0");
        private static readonly FieldInfo SolutionField = Deobf.Field(typeof(PuzzleCompletionScreen), "solution_0");

        public override void Build(GraphBuilder b)
        {
            var s = GameState.TopScreen() as PuzzleCompletionScreen;
            if (s == null) return;
            b.PushContext(Loc.T("editor.stats"), positions: false);
            StatRow(b, "pc.cycles", () => ScoreManager.locString_0.ToString(), () => Stat(s, 0));
            StatRow(b, "pc.size", () => ScoreManager.locString_1.ToString(), () => Stat(s, 1));
            StatRow(b, "pc.activity", () => ScoreManager.locString_2.ToString(), () => Stat(s, 2));
            // The Leaderboards / Test Run Data view flip (mouse-only in the game): a toggle
            // announcing the tab now shown. The views are drawn content; the flip is parity
            // for a sighted co-viewer and the entry point for reading them later.
            b.AddItem(ControlId.Structural("pc.tab"), new NodeVtable
            {
                ControlType = ControlTypes.Toggle,
                SpeaksOwnPosition = true,
                Announcements = new[]
                {
                    new NodeAnnouncement(() => ActiveTab(s), kind: AnnouncementKinds.Label),
                },
                StateText = () => ActiveTab(s),
                OnActivate = () => FlipTab(s),
            });
            // The Record Solution GIF button (mouse-only in the game): the exact click handler.
            b.AddItem(ControlId.Structural("pc.gif"), new NodeVtable
            {
                ControlType = ControlTypes.Button,
                SpeaksOwnPosition = true,
                Announcements = new[]
                {
                    new NodeAnnouncement(() => GameText.T("Record Solution GIF     ").Trim(),
                        kind: AnnouncementKinds.Label),
                },
                OnActivate = () => RecordGif(s),
            });
            // The leave button (mouse-only art aside, the game's own Enter shortcut): label
            // read live — Redshift-type puzzles say Return to VirtualNetwork+ — activation
            // replicating the game's click path exactly (leave sound, the editor's teardown
            // hook, the double pop past the editor).
            b.AddItem(ControlId.Structural("pc.leave"), new NodeVtable
            {
                ControlType = ControlTypes.Button,
                SpeaksOwnPosition = true,
                Announcements = new[]
                {
                    new NodeAnnouncement(() => LeaveLabel(s), kind: AnnouncementKinds.Label),
                },
                OnActivate = () => Leave(s),
            });
            b.PopContext();
            BuildLeaderboards(b, s);
        }

        // The Escape option is announced once on entry (user rule) — leaving is now the
        // browsable pc.leave button, so Enter is no longer named here.
        public override void OnFocus()
        {
            base.OnFocus();
            try
            {
                Speech.Tts.Speak(Loc.T("editor.complete.options", new
                {
                    resume = GameText.T("Continue Editing"),
                }));
                // The game's undrawn native shortcut: Ctrl+C copies the per-run score table.
                Speech.Tts.Speak(Loc.T("editor.complete.copy"));
            }
            catch { }
        }

        /// <summary>bool_0 picks the right-hand view: false = Leaderboards (histograms), true =
        /// Test Run Data (the per-run table) — the game's clickable tab is always the INACTIVE
        /// one, so the toggle speaks what is now SHOWN.</summary>
        private static string ActiveTab(PuzzleCompletionScreen s)
        {
            try
            {
                bool data = TabField != null && (bool)TabField.GetValue(s);
                return GameText.T(data ? "Test Run Data" : "Leaderboards");
            }
            catch { return null; }
        }

        private static void FlipTab(PuzzleCompletionScreen s)
        {
            try { TabField?.SetValue(s, !(bool)TabField.GetValue(s)); }
            catch (Exception ex) { Log.Error("[complete] tab flip failed", ex); }
        }

        // The puzzle-details registry (Puzzles.smethod_2) is INTERNAL game-side — resolved via
        // Deobf like any private member (the Puzzles type name is preserved live).
        private static readonly MethodInfo PuzzleDetailsMethod = ResolvePuzzleDetails();

        private static MethodInfo ResolvePuzzleDetails()
        {
            try
            {
                var t = typeof(Puzzle).Assembly.GetType("Puzzles");
                return t != null ? Deobf.Method(t, "smethod_2") : null;
            }
            catch { return null; }
        }

        private static string LeaveLabel(PuzzleCompletionScreen s)
        {
            try
            {
                bool vnet = false;
                var solution = SolutionField?.GetValue(s) as Solution;
                if (solution != null && PuzzleDetailsMethod != null)
                {
                    var details = PuzzleDetailsMethod.Invoke(null, new object[] { solution.method_0() });
                    var mode = details == null ? null : Deobf.Field(details.GetType(), "genum145_0");
                    vnet = mode != null && Convert.ToInt32(mode.GetValue(details)) != 0;
                }
                return GameText.T(vnet ? "Return to VirtualNetwork+" : "Return to Desktop");
            }
            catch { return GameText.T("Return to Desktop"); }
        }

        private static void Leave(PuzzleCompletionScreen s)
        {
            try
            {
                var editor = EditorField?.GetValue(s) as EditorScreen;
                if (editor == null) return;
                try { GClass45.soundsNamespace_0.sound_34.smethod_1(1f); } catch { }
                editor.method_19();
                GameApi.PopScreen();
                GameApi.PopScreen();
            }
            catch (Exception ex) { Log.Error("[complete] leave failed", ex); }
        }

        private static void RecordGif(PuzzleCompletionScreen s)
        {
            try
            {
                var solution = SolutionField?.GetValue(s) as Solution;
                var editor = EditorField?.GetValue(s) as EditorScreen;
                if (solution == null || editor == null) return;
                GameApi.PushScreen(new GifRecorderScreen(solution, editor.int_4));
            }
            catch (Exception ex) { Log.Error("[complete] gif failed", ex); }
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
