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

            // The map's NETWORK LOGO decal (meta.texture_0, drawn on every network's
            // backdrop) is baked art — a lettered brand reaches only sighted players, and
            // on the mystery networks the logo says what no spoken string does (the game
            // titles them "UNKNOWN NETWORK"). Transcribed per puzzle; no entry = no row.
            if (NetworkLogoText() != null)
                b.AddItem(ControlId.Structural("ed.logo"), new NodeVtable
                {
                    ControlType = ControlTypes.Text,
                    Announcements = new[]
                    {
                        new NodeAnnouncement(() => Loc.T("editor.logo", new { text = NetworkLogoText() }),
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
            // The accessible Show Goal: opens the goal popup — the F1 view as text (required
            // files, register goal readouts, and the special-puzzle panels via PanelCapture).
            // NORMAL MODE ONLY: battles and the sandbox draw no Show Goal button and their F1
            // is inert (the game's own gate — EditorScreen draws it only for mode 0).
            if (GameMode(e) == 0)
                b.AddItem(ControlId.Structural("ed.goalbtn"), new NodeVtable
                {
                    ControlType = ControlTypes.Button,
                    Announcements = new[]
                    {
                        new NodeAnnouncement(() => GameText.T("Show Goal"), kind: AnnouncementKinds.Label),
                    },
                    OnActivate = RequestGoalPopup,
                });
            b.PopContext();
        }

        // Logo transcriptions are language-invariant (one set of map textures ships for all
        // six game languages), so they live here rather than in ui.json. Keyed by puzzle id
        // (meta.string_0); add an entry when an audit finds a logo whose lettering says
        // something no spoken title/description carries.
        private static readonly System.Collections.Generic.Dictionary<string, string> NetworkLogos =
            new System.Collections.Generic.Dictionary<string, string>
            {
                // PB008 "UNKNOWN NETWORK 1": the brand + department line, screenshot-verified
                // 2026-08-23 — the art is the only place the network names itself.
                { "PB008", "НГТУ, ОТДЕЛ ПРИКЛАДНОЙ СЕМИОТИКИ" },
                // PB010B: the Workhouse brand plus the slogan the spoken title lacks,
                // screenshot-verified 2026-08-23.
                { "PB010B", "Workhouse. Easy work. Easy money." },
                // PB012: the bank's numeral mark + full brand name (the spoken title drops
                // "National"), screenshot-verified 2026-08-23.
                { "PB012", "1, Equity First National Bank" },
                // PB014: the battle map's backdrop brand — the "News Network" subline is
                // lettering the spoken "KGOG-TV (Programming Hub)" lacks,
                // screenshot-verified 2026-08-24.
                { "PB014", "KGOG News Network" },
            };

        private static string NetworkLogoText()
        {
            try
            {
                var meta = Meta(Editor);
                string text;
                return meta != null && NetworkLogos.TryGetValue(meta.string_0, out text) ? text : null;
            }
            catch { return null; }
        }

        // ---- the goal popup: everything the game's F1 view shows, as bare terse rows. Opening
        // forces the game's own show-goal flag (PanelCapture.ForceGoal — the screen visibly flips
        // to the F1 view, same as a sighted player holding the key) and arms the panel-text
        // capture; the open DEFERS two ticks so a full goal-view frame has been drawn and
        // published before the first row speaks. Enter/Backspace/Escape close. ----

        private bool _goalPopup;
        private int _goalPopupPending;
        private ControlId _goalReturnFocus;

        private void RequestGoalPopup()
        {
            if (_goalPopup || _goalPopupPending > 0) return;
            // F1 opens from anywhere — closing returns to wherever the user was.
            _goalReturnFocus = (Navigation.Active as GraphNavigator)?.FocusedNodeId;
            _goalPopupPending = 2;
            Patches.PanelCapture.SetArmed(true, forceGoal: true);
        }

        private void CloseGoalPopup()
        {
            _goalPopup = false;
            _goalPopupPending = 0;
            Patches.PanelCapture.SetArmed(false, false);
            Navigation.FocusNode(_goalReturnFocus ?? ControlId.Structural("ed.goalbtn"));
        }

        private bool BuildGoalPopup(GraphBuilder b)
        {
            var rows = GoalRowTexts(Editor);
            if (rows.Count == 0) return false;
            // Bare rows, no context, no position counts — the file-popup precedent (user rule).
            b.BeginStop("goalpop");
            for (int i = 0; i < rows.Count; i++)
            {
                var row = rows[i];
                var id = ControlId.Structural("ed.gpop." + i);
                b.AddItem(id, new NodeVtable
                {
                    ControlType = ControlTypes.Text,
                    SpeaksOwnPosition = true,
                    Announcements = new[]
                    {
                        new NodeAnnouncement(() => row.Text, kind: AnnouncementKinds.Label),
                    },
                    // A goal-FILE row opens the same values popup a live file row does —
                    // every value past the row's 60-value cap browsable one per row.
                    OnActivate = row.Required >= 0
                        ? () => OpenGoalFilePopup(row.Host, row.Required, id)
                        : (Action)CloseGoalPopup,
                    OnSecondary = CloseGoalPopup,
                });
            }
            return true;
        }

        /// <summary>One popup row: the text, and for goal-FILE rows the (host, required-file)
        /// indexes the values popup re-resolves by — the sim rebuilds every frame while
        /// editing, so nothing from it is cached.</summary>
        private struct GoalRow
        {
            public string Text;
            public int Host;
            public int Required; // -1 on every non-file row

            public static GoalRow Plain(string text)
                => new GoalRow { Text = text, Host = -1, Required = -1 };
        }

        /// <summary>The popup's rows: required files (the model, structured), the register goal
        /// readouts and host statuses (the GClass298 virtual API — vmethod_9's consume arg stays
        /// false, a pure read; per-puzzle logics throw for registers they don't own), then the
        /// captured panel lines. All game text.</summary>
        private System.Collections.Generic.List<GoalRow> GoalRowTexts(EditorScreen e)
        {
            var rows = new System.Collections.Generic.List<GoalRow>();
            try
            {
                var sim = TheSim(e);
                if (sim == null) return rows;
                for (int h = 0; h < sim.list_0.Count; h++)
                {
                    var host = sim.list_0[h];
                    // Hosts still hidden UNDER the goal view keep their secrets there too.
                    if (HostHidden(host, true)) continue;
                    for (int r = 0; r < host.list_0.Count; r++)
                    {
                        var required = host.list_0[r];
                        string id = required.maybe_0.method_0()
                            ? required.maybe_0.method_2().ToString() : GameText.T("NEW");
                        var values = new System.Collections.Generic.List<string>();
                        int total = required.exaValue_0.Length;
                        for (int i = 0; i < total && i < FileValuesSpoken; i++)
                            values.Add(required.exaValue_0[i].method_2(true));
                        string joined = string.Join(", ", values);
                        if (total > FileValuesSpoken)
                            joined += " " + Loc.T("editor.file.more", new { n = total - FileValuesSpoken });
                        rows.Add(new GoalRow
                        {
                            Text = Loc.T("editor.goal.file", new
                            {
                                id,
                                host = HostName(host, true),
                                values = joined,
                            }),
                            Host = h,
                            Required = r,
                        });
                    }
                }
                var logic = sim.method_43();
                if (logic != null)
                    foreach (var host in sim.list_0)
                    {
                        if (HostHidden(host, true)) continue;
                        foreach (var reg in host.list_2)
                        {
                            string id = RegName(reg);
                            string value = null;
                            if (reg.genum160_0 == (GEnum160)0) // the game only shows values on readable plates
                                try { value = logic.vmethod_9(reg, true, e.method_24(), false).method_2(true); }
                                catch { } // per-puzzle logics throw for registers they don't own
                            else if (reg.genum160_0 == (GEnum160)1)
                                value = Loc.T("editor.reg.writeonly");
                            rows.Add(GoalRow.Plain(string.IsNullOrEmpty(value)
                                ? Loc.T("editor.goal.register.plain", new { id, host = HostName(host, true) })
                                : Loc.T("editor.goal.register", new { id, host = HostName(host, true), value })));
                        }
                        try
                        {
                            // Prefix with the MAP-side name: HostName(goal:true) would resolve to
                            // this same override, reading "X: X".
                            var status = logic.vmethod_10(host, true);
                            if (status.method_0())
                                rows.Add(GoalRow.Plain(HostName(host) + ": " + GameText.Speech(status.method_2())));
                        }
                        catch { }
                    }
                AddHighwaySignRows(logic, rows);
                foreach (var line in Patches.PanelCapture.Lines)
                    rows.Add(GoalRow.Plain(GameText.Speech(line)));
            }
            catch { }
            return rows;
        }

        // The highway sign (SFCTA) draws its content as glyph SPRITES from a font atlas — no
        // text for PanelCapture to record — and in the goal view those glyphs render the TARGET
        // message. Read it from the model instead: HighwaySign.string_1, the sign as one flat
        // 3x9 string. Rows speak with their 0-based index — the same row number a #DATA write
        // addresses — split exactly as the sign displays them (words may break across rows).
        private static readonly FieldInfo SignTargetField =
            Deobf.Field(typeof(SpecialPuzzleLogics.HighwaySign), "string_1");
        // The sign's geometry (9 columns x 3 rows, static readonly game-side) — drawn as a
        // countable cell grid in the DIGICAM FEED panel, so its dimensions are sighted-visible;
        // the leading row states them (columns are the 0..8 range a #DATA write addresses).
        private static readonly FieldInfo SignColsField =
            Deobf.Field(typeof(SpecialPuzzleLogics.HighwaySign), "int_0");
        private static readonly FieldInfo SignRowsField =
            Deobf.Field(typeof(SpecialPuzzleLogics.HighwaySign), "int_1");

        private static void AddHighwaySignRows(GClass298 logic, System.Collections.Generic.List<GoalRow> rows)
        {
            try
            {
                var sign = logic as SpecialPuzzleLogics.HighwaySign;
                if (sign == null || SignTargetField == null) return;
                var text = SignTargetField.GetValue(sign) as string;
                if (text == null) return;
                int cols = 9, signRows = 3;
                try { if (SignColsField != null) cols = (int)SignColsField.GetValue(null); } catch { }
                try { if (SignRowsField != null) signRows = (int)SignRowsField.GetValue(null); } catch { }
                rows.Add(GoalRow.Plain(Loc.T("editor.goal.sign.size", new { rows = signRows, cols })));
                for (int row = 0; row * cols < text.Length; row++)
                {
                    int len = Math.Min(cols, text.Length - row * cols);
                    string line = text.Substring(row * cols, len);
                    // Column span (0-based, the same numbers a #DATA write addresses) — the
                    // drawn sign shows WHERE in the row the words sit, so the rows say it too.
                    int first = -1, last = -1;
                    for (int i = 0; i < line.Length; i++)
                        if (line[i] != ' ') { if (first < 0) first = i; last = i; }
                    if (first < 0)
                        rows.Add(GoalRow.Plain(Loc.T("editor.goal.sign", new { row, text = Loc.T("text.blank") })));
                    else if (first == last)
                        rows.Add(GoalRow.Plain(Loc.T("editor.goal.sign.col1", new
                        { row, col = first, text = CharSpeech(line[first].ToString()) })));
                    else
                        rows.Add(GoalRow.Plain(Loc.T("editor.goal.sign.cols", new
                        { row, lo = first, hi = last, text = line.Substring(first, last - first + 1) })));
                }
            }
            catch { }
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
    }
}
