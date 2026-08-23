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
        // ---- the highway sign as an explorable GRID (user design, 2026-08-22): a Tab stop
        // scoped to the SELECTED host like links/files/registers (user rule, 2026-08-22 —
        // host-scoped info always follows selection, never a general stop): present only when
        // the selected host owns the sign's registers. One node per cell, up/down = rows with
        // the column preserved, left/right = columns.
        // Cells speak BARE — the character, or "blank" — position is implicit in the
        // navigation, like the drawn cell grid a sighted player counts. Values are the LIVE
        // sign (per-frame sim rebuild while editing = the initial road-work message;
        // persistent while armed = whatever the program has written so far), re-resolved on
        // every announce and declared Live, so the focused cell re-announces when a
        // #DATA/#CLRS write changes it — the hardware-register precedent. The game's drawn
        // row/column/character protocol status is deliberately NOT voiced (zine-documented;
        // user decision, 2026-08-22). ----

        private static readonly FieldInfo SignCellsField =
            Deobf.Field(typeof(SpecialPuzzleLogics.HighwaySign), "int_3");

        private void BuildSign(GraphBuilder b, EditorScreen e)
        {
            var sign = LiveSign(e);
            if (sign == null) return;
            var sim = TheSim(e);
            if (sim == null || _selectedHost >= sim.list_0.Count) return;
            var host = sim.list_0[_selectedHost];
            try { if (!host.list_2.Contains(sign.gclass265_0)) return; } catch { return; }
            if (HostHidden(host, false)) return; // hidden host = the sign is off the map too
            int cols = 9, signRows = 3;
            try { if (SignColsField != null) cols = (int)SignColsField.GetValue(null); } catch { }
            try { if (SignRowsField != null) signRows = (int)SignRowsField.GetValue(null); } catch { }
            b.BeginStop("sign");
            b.PushContext(Loc.T("editor.sign", new { host = HostName(host) }), positions: false);
            for (int r = 0; r < signRows; r++)
            {
                b.StartRow("sign"); // SHARED row key = column-preserving up/down (VerticalTarget)
                for (int c = 0; c < cols; c++)
                {
                    int cell = r * cols + c;
                    b.AddItem(ControlId.Structural("ed.sign." + r + "." + c), new NodeVtable
                    {
                        ControlType = ControlTypes.Text,
                        SpeaksOwnPosition = true, // bare cells, no counts (user rule)
                        Announcements = new[]
                        {
                            new NodeAnnouncement(() => CellText(cell), live: true,
                                kind: AnnouncementKinds.Label),
                        },
                    });
                }
                b.EndRow();
            }
            b.PopContext();
        }

        /// <summary>Re-resolved per call — the edit-time per-frame sim rebuild replaces the
        /// HighwaySign instance (and its cell array) constantly; captured instances go stale.</summary>
        private static SpecialPuzzleLogics.HighwaySign LiveSign(EditorScreen e)
        {
            try { return TheSim(e)?.method_43() as SpecialPuzzleLogics.HighwaySign; }
            catch { return null; }
        }

        private static string CellText(int cell)
        {
            try
            {
                var sign = LiveSign(Editor);
                if (sign == null || SignCellsField == null) return null;
                var cells = SignCellsField.GetValue(sign) as int[];
                if (cells == null || cell >= cells.Length) return null;
                var font = SpecialPuzzleLogics.HighwaySign.string_0;
                return CharSpeech(font[Math.Max(0, Math.Min(cells[cell], font.Length - 1))]);
            }
            catch { return null; }
        }

        // The sign font is " A-Z 0-9 . ? !" — letters/digits speak as-is; the space cell and
        // lone punctuation get speakable names (a bare "." is silence to most TTS voices).
        private static string CharSpeech(string ch)
        {
            switch (ch)
            {
                case " ": return Loc.T("text.blank");
                case ".": return Loc.T("text.dot");
                case "?": return Loc.T("text.question");
                case "!": return Loc.T("text.exclamation");
                default: return ch;
            }
        }
    }
}
