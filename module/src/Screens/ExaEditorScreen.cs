using System;
using System.Reflection;
using ExaAccess.Game;
using ExaAccess.Localization;
using ExaAccess.UI;
using ExaAccess.UI.Graph;

namespace ExaAccess.Screens
{
    /// <summary>
    /// The EXA editor (game type EditorScreen, name-preserved) — the puzzle-solving screen and the
    /// mod's long game (see "EXA editor decoded" in CLAUDE.md). TASK 1 of the build-out: the
    /// read-only browse skeleton — three Tab stops over the fully-text parts of the screen: the
    /// task panel (puzzle description + live goal checklist), the score readouts (test run,
    /// cycles/size/activity — each carrying the game's own explainer tooltip on Space), and the
    /// solution header. Values read live at announce time; nothing is marked live-watched (cycles
    /// change 50×/second while running — announce-on-focus only). Escape stays native (the game's
    /// own leave-screen/reset behavior).
    /// </summary>
    public sealed class ExaEditorScreen : Screen
    {
        public ExaEditorScreen() { Wrap = true; }

        public override string Key => "editor";
        public override bool IsActive() => GameState.TopScreen() is EditorScreen;
        public override object InitialFocusStop => "task";

        public override string ScreenName
        {
            get
            {
                // The puzzle's own title; the generic label if anything is missing.
                try
                {
                    string title = GameText.Speech(Meta(Editor)?.locString_0.ToString());
                    if (!string.IsNullOrEmpty(title)) return title;
                }
                catch { }
                return Loc.T("screen.EditorScreen");
            }
        }

        // The private half (EditorScreen is name-preserved — direct namemap hits):
        // sim_0 = the live sim (rebuilt every frame while editing), maybe_1 = the size score,
        // method_23 = the effective test-run number while running.
        private static readonly FieldInfo SimField = Deobf.Field(typeof(EditorScreen), "sim_0");
        private static readonly FieldInfo SizeField = Deobf.Field(typeof(EditorScreen), "maybe_1");
        private static readonly MethodInfo RunNumberMethod = Deobf.Method(typeof(EditorScreen), "method_23");

        private static EditorScreen Editor => GameState.TopScreen() as EditorScreen;

        private static Sim TheSim(EditorScreen e)
        {
            try { return SimField?.GetValue(e) as Sim; }
            catch { return null; }
        }

        // Puzzle → metadata is an extension on the INTERNAL static class Puzzles — invisible to
        // the compiler, reached by reflection (name-preserved class, so Deobf resolves directly).
        private static readonly MethodInfo PuzzleMetaMethod =
            Deobf.Method(typeof(Puzzle).Assembly.GetType("Puzzles"), "smethod_2");

        /// <summary>The puzzle's metadata blob (title, description, size limit).</summary>
        private static GClass361 Meta(EditorScreen e)
        {
            try
            {
                if (e == null || PuzzleMetaMethod == null) return null;
                return PuzzleMetaMethod.Invoke(null, new object[] { e.solution_0.method_0() }) as GClass361;
            }
            catch { return null; }
        }

        private static bool Editing(EditorScreen e)
        {
            try { return e != null && e.method_2(); }
            catch { return true; }
        }

        public override void Build(GraphBuilder b)
        {
            var e = Editor;
            if (e == null) return;
            BuildTask(b, e);
            BuildStats(b, e);
            BuildSolution(b, e);
        }

        // ---- the task panel: description + the live goal checklist ----

        private void BuildTask(GraphBuilder b, EditorScreen e)
        {
            b.BeginStop("task");
            b.PushContext(Loc.T("editor.task"), positions: false);
            b.AddItem(ControlId.Structural("ed.desc"), new NodeVtable
            {
                ControlType = ControlTypes.Text,
                Announcements = new[]
                {
                    new NodeAnnouncement(() => GameText.Speech(Meta(Editor)?.locString_2.ToString()),
                        kind: AnnouncementKinds.Label),
                },
            });

            var sim = TheSim(e);
            if (sim != null)
                for (int i = 0; i < sim.list_2.Count; i++)
                {
                    int index = i;
                    b.AddItem(ControlId.Structural("ed.goal." + i), new NodeVtable
                    {
                        ControlType = ControlTypes.Text,
                        Announcements = new[]
                        {
                            new NodeAnnouncement(() => GoalLabel(index), kind: AnnouncementKinds.Label),
                            new NodeAnnouncement(() => GoalState(index), kind: AnnouncementKinds.Value),
                        },
                    });
                }
            b.PopContext();
        }

        // Mirrors the checklist draw: label (+ " (n/m)" when the goal has progress), and the
        // tick/cross state — neutral until the sim has actually run a cycle.
        private static string GoalLabel(int index)
        {
            try
            {
                var e = Editor;
                var sim = TheSim(e);
                if (sim == null || index >= sim.list_2.Count) return null;
                var goal = sim.list_2[index];
                string label = GameText.Speech(goal.imethod_0());
                var state = goal.imethod_1(sim, sim.list_5);
                if (state.int_1 > 1) label = label + " (" + state.int_0 + "/" + state.int_1 + ")";
                return label;
            }
            catch { return null; }
        }

        private static string GoalState(int index)
        {
            try
            {
                var e = Editor;
                var sim = TheSim(e);
                if (sim == null || index >= sim.list_2.Count) return null;
                bool neutral = !e.method_0() || sim.method_52() < 1;
                var state = sim.list_2[index].imethod_1(sim, sim.list_5);
                if (state.genum152_0 == (GEnum152)0 || neutral) return null;
                return state.genum152_0 == (GEnum152)1 ? Loc.T("value.complete") : Loc.T("value.failed");
            }
            catch { return null; }
        }

        // ---- the score readouts, each with the game's own explainer as its tooltip ----

        private void BuildStats(GraphBuilder b, EditorScreen e)
        {
            b.BeginStop("stats");
            b.PushContext(Loc.T("editor.stats"), positions: false);

            StatRow(b, "ed.run", () => GameText.T("Test Run"), () =>
            {
                var ed = Editor;
                if (ed == null) return null;
                int run = ed.int_4;
                if (!Editing(ed))
                    try { run = (int)RunNumberMethod.Invoke(ed, null); } catch { }
                return (run + 1) + " / 100";
            }, () => GameText.TSpeech("To complete a task you must complete all 100 test runs. You can change the initial test run to more easily debug problems that only occur on specific test runs."));

            StatRow(b, "ed.cycles", () => ScoreManager.locString_0.ToString(), () =>
            {
                var ed = Editor;
                var sim = TheSim(ed);
                return Editing(ed) || sim == null ? "0" : sim.method_52().ToString();
            }, () => GameText.TSpeech("Your cycles score is the number of turns it takes your EXAs to complete the task."));

            StatRow(b, "ed.size", () => ScoreManager.locString_1.ToString(), () =>
            {
                var ed = Editor;
                if (ed == null) return null;
                try
                {
                    var size = (Maybe<int>)SizeField.GetValue(ed);
                    int limit = Meta(ed)?.int_2 ?? 0;
                    return size.method_0() ? size.method_2() + " / " + limit : GameText.T("N/A");
                }
                catch { return null; }
            }, () =>
            {
                int limit = Meta(Editor)?.int_2 ?? 0;
                return string.Format(GameText.TSpeech("Your size score is the total number of instructions in all of your EXAs, including MARK pseudo-instructions.\n\nIn this task your size may not exceed {0}."), limit);
            });

            StatRow(b, "ed.activity", () => ScoreManager.locString_2.ToString(), () =>
            {
                var ed = Editor;
                var sim = TheSim(ed);
                return Editing(ed) || sim == null ? "0" : sim.method_53().ToString();
            }, () => GameText.TSpeech("Your activity score is the number of times EXAs you control execute LINK or KILL instructions."));

            b.PopContext();
        }

        private static void StatRow(GraphBuilder b, string id, Func<string> label, Func<string> value, Func<string> tooltip)
        {
            b.AddItem(ControlId.Structural(id), new NodeVtable
            {
                ControlType = ControlTypes.Text,
                Announcements = new[]
                {
                    new NodeAnnouncement(label, kind: AnnouncementKinds.Label),
                    new NodeAnnouncement(value, kind: AnnouncementKinds.Value),
                },
                OnTooltip = () =>
                {
                    try { Speech.Tts.Speak(tooltip()); } catch { }
                },
            });
        }

        // ---- the solution header ----

        private void BuildSolution(GraphBuilder b, EditorScreen e)
        {
            b.BeginStop("solution");
            b.PushContext(Loc.T("editor.solution"), positions: false);
            b.AddItem(ControlId.Structural("ed.solname"), new NodeVtable
            {
                ControlType = ControlTypes.Text,
                Announcements = new[]
                {
                    new NodeAnnouncement(() => Loc.T("editor.solution.name"), kind: AnnouncementKinds.Label),
                    new NodeAnnouncement(() =>
                    {
                        try { return Editor?.solution_0.string_0; }
                        catch { return null; }
                    }, kind: AnnouncementKinds.Value),
                },
            });
            b.AddItem(ControlId.Structural("ed.exacount"), new NodeVtable
            {
                ControlType = ControlTypes.Text,
                Announcements = new[]
                {
                    new NodeAnnouncement(() => Loc.T("editor.exas"), kind: AnnouncementKinds.Label),
                    new NodeAnnouncement(() =>
                    {
                        try { return Editor?.solution_0.list_0.Count.ToString(); }
                        catch { return null; }
                    }, kind: AnnouncementKinds.Value),
                },
            });
            b.PopContext();
        }
    }
}
