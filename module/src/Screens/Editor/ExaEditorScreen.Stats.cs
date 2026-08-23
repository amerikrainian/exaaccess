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

            // The test-run row is ADJUSTABLE (Left/Right, PgUp/PgDn) while editing — the
            // keyboard version of the game's mouse-only arrows, riding the game's own request
            // path (maybe_10, clamped + rebuilt at the next tick) — and Enter opens the game's
            // inline TYPED field (bool_8): the row flips to a text field, digits flow to the
            // game's widget whose parse applies live, Enter or leaving closes. While running
            // the row reads the current run and is inert.
            if (!RunFieldOpen(e))
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
                        try { RunRequestField?.SetValue(ed, (Maybe<int>)(ed.int_4 + sign * (large ? 10 : 1))); }
                        catch { }
                    },
                    OnActivate = OpenRunField,
                    OnTooltip = () => Speech.Tts.Speak(GameText.TSpeech("To complete a task you must complete all 100 test runs. You can change the initial test run to more easily debug problems that only occur on specific test runs.")),
                });
            else
                b.AddItem(ControlId.Structural("ed.run"), new NodeVtable
                {
                    ControlType = ControlTypes.TextField,
                    Announcements = new[]
                    {
                        new NodeAnnouncement(() => GameText.T("Test Run"), kind: AnnouncementKinds.Label),
                        new NodeAnnouncement(RunFieldValue, kind: AnnouncementKinds.Value),
                    },
                    TextEntry = true,
                    TextEchoCaps = false,
                    TextValue = RunFieldValue,
                    OnSelect = () => { }, // Enter on the slider already armed the game's field
                    OnActivate = () => CloseRunField(announce: true),
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

        // The game's typed test-run field: bool_8 = the open flag, string_0 = its text buffer,
        // maybe_10 = the pending run request the tick clamps and applies (method_25 + method_45).
        private static readonly FieldInfo RunFieldFlag = Deobf.Field(typeof(EditorScreen), "bool_8");
        private static readonly FieldInfo RunFieldText = Deobf.Field(typeof(EditorScreen), "string_0");
        private static readonly FieldInfo RunRequestField = Deobf.Field(typeof(EditorScreen), "maybe_10");

        private static bool RunFieldOpen(EditorScreen e)
        {
            try { return e != null && RunFieldFlag != null && (bool)RunFieldFlag.GetValue(e); }
            catch { return false; }
        }

        private static string RunFieldValue()
        {
            try { return (string)RunFieldText.GetValue(Editor); }
            catch { return null; }
        }

        private static void OpenRunField()
        {
            var e = Editor;
            if (e == null || !Editing(e) || RunFieldOpen(e)) return;
            try
            {
                // The game's click on the number, exactly (close fields, flag, blink, clear).
                Invoke(CloseFieldsMethod, e);
                RunFieldFlag.SetValue(e, true);
                GClass288.smethod_1();
                RunFieldText.SetValue(e, string.Empty);
                Speech.Tts.Speak(Loc.T("editor.run.type"), interrupt: true);
            }
            catch (Exception ex) { Log.Error("[editor] run field open failed", ex); }
        }

        /// <summary>The typed parse already applied itself (the game re-parses into maybe_10 as
        /// the text changes) — closing is just the flag, like Enter or a click-away.</summary>
        private static void CloseRunField(bool announce)
        {
            var e = Editor;
            if (e == null || !RunFieldOpen(e)) return;
            try
            {
                RunFieldFlag.SetValue(e, false);
                if (announce) Speech.Tts.Speak(RunText(), interrupt: true);
            }
            catch (Exception ex) { Log.Error("[editor] run field close failed", ex); }
        }

        private static string RunText()
        {
            var ed = Editor;
            if (ed == null) return null;
            int run = ed.int_4;
            if (!Editing(ed))
                try { run = (int)RunNumberMethod.Invoke(ed, null); } catch { }
            else
                // A request the tick hasn't applied yet (our adjust, or the typed field's live
                // parse) IS the value to speak — mirror the clamp it will get.
                try
                {
                    var pending = (Maybe<int>)RunRequestField.GetValue(ed);
                    if (pending.method_0())
                        run = Math.Max(0, Math.Min(GClass68.int_0 - 1, pending.method_2()));
                }
                catch { }
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
