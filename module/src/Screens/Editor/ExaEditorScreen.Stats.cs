using System;
using System.Reflection;
using ExaAccess.Game;
using ExaAccess.Localization;
using ExaAccess.UI;
using ExaAccess.UI.Graph;

namespace ExaAccess.Screens
{
    public sealed partial class ExaEditorScreen
    {
        // ---- the score readouts, each with the game's own explainer as its tooltip ----

        private void BuildStats(GraphBuilder b, EditorScreen e)
        {
            b.BeginStop("stats");
            b.PushContext(Loc.T("editor.stats"), positions: false);

            // The test-run row is ADJUSTABLE (Left/Right) while editing — the keyboard version of
            // the game's mouse-only arrows; while running it reads the current run and is inert.
            b.AddItem(ControlId.Structural("ed.run"), new NodeVtable
            {
                ControlType = ControlTypes.Slider,
                Announcements = new[]
                {
                    new NodeAnnouncement(() => GameText.T("Test Run"), kind: AnnouncementKinds.Label),
                    new NodeAnnouncement(RunText, kind: AnnouncementKinds.Value),
                },
                StateText = RunText,
                OnAdjust = (sign, large) =>
                {
                    var ed = Editor;
                    if (ed == null || !Editing(ed)) return;
                    ed.int_4 = Math.Max(0, Math.Min(99, ed.int_4 + sign * (large ? 10 : 1)));
                },
                OnTooltip = () => Speech.Tts.Speak(GameText.TSpeech("To complete a task you must complete all 100 test runs. You can change the initial test run to more easily debug problems that only occur on specific test runs.")),
            });

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

        private static string RunText()
        {
            var ed = Editor;
            if (ed == null) return null;
            int run = ed.int_4;
            if (!Editing(ed))
                try { run = (int)RunNumberMethod.Invoke(ed, null); } catch { }
            return (run + 1) + " / 100";
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
            // The solution name is EDITABLE, typing-first: landing arms the game's own inline
            // field (append-style; typing/Backspace flow, echoed); Enter or leaving commits with
            // the game's undo snapshot.
            b.AddItem(ControlId.Structural("ed.solname"), new NodeVtable
            {
                ControlType = ControlTypes.TextField,
                Announcements = new[]
                {
                    new NodeAnnouncement(() => Loc.T("editor.solution.name"), kind: AnnouncementKinds.Label),
                    new NodeAnnouncement(() =>
                    {
                        try { return Editor?.solution_0.string_0; }
                        catch { return null; }
                    }, kind: AnnouncementKinds.Value),
                },
                TextEntry = true,
                TextEchoCaps = false, // the game font uppercases inserts
                TextValue = () =>
                {
                    try { return Editor?.solution_0.string_0; }
                    catch { return null; }
                },
                OnSelect = () => ArmName(Editor),
                OnActivate = () => CommitName(Editor),
            });
            b.AddItem(ControlId.Structural("ed.newexa"), new NodeVtable
            {
                ControlType = ControlTypes.Button,
                Announcements = new[]
                {
                    new NodeAnnouncement(() => GameText.T("Create New EXA"), kind: AnnouncementKinds.Label),
                },
                OnActivate = CreateExa,
            });
            b.AddItem(ControlId.Structural("ed.exacount"), new NodeVtable
            {
                ControlType = ControlTypes.Text,
                Announcements = new[]
                {
                    new NodeAnnouncement(() => Loc.T("editor.exas"), kind: AnnouncementKinds.Label),
                    new NodeAnnouncement(ExaCountText, kind: AnnouncementKinds.Value),
                },
            });
            b.PopContext();
        }
    }
}
