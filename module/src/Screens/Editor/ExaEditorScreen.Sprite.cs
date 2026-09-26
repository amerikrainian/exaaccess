using System;
using Echopunks.Game;
using Echopunks.Localization;
using Echopunks.UI;
using Echopunks.UI.Graph;

namespace Echopunks.Screens
{
    public sealed partial class ExaEditorScreen
    {
        // ---- the sprite editor (sandbox only): the EXA window's G-row 10x10 pixel grid as
        // an explorable grid stop — the sign-grid shape, but WRITABLE. Scoped to the CODE
        // stop's focused program (the grid lives in that EXA's window; Ctrl+Up/Down moves
        // both together). While EDITING, cells read and Enter-toggle SolutionExa.bool_0 —
        // the authored sprite, saved with the solution — through the game's own paint path
        // (undo snapshot, set, mark dirty; Ctrl+Z restores). While ARMED, cells read the
        // FOLLOWED instance's live pattern (bool_4 — what GP writes repaint) and toggling
        // refuses, like the drawn grid whose paint callback exists only at edit time.
        // Cells speak bare on/off (grid cells bare, position implicit — the sign rule);
        // values re-resolve per announce and are Live, so a GP write re-announces the
        // focused cell, and a toggle's flip speaks through the same watch. Cell (r,c) is
        // the drawn window grid cell-for-cell: index = r*10+c, row 0 at the top — the same
        // index a GP pixel write addresses. ----

        private void BuildSprite(GraphBuilder b, EditorScreen e)
        {
            if (!SandboxMode(e)) return;
            var program = TargetCodeExa(e);
            if (program == null) return;
            int number = 0;
            try { number = program.method_0(); } catch { return; }
            b.BeginStop("sprite");
            b.PushContext(Loc.T("editor.sprite", new { exa = program.string_0 }), positions: false);
            for (int r = 0; r < 10; r++)
            {
                b.StartRow("sprite"); // SHARED row key = column-preserving up/down (VerticalTarget)
                for (int c = 0; c < 10; c++)
                {
                    int cell = r * 10 + c;
                    b.AddItem(ControlId.Structural("ed.spr." + r + "." + c), new NodeVtable
                    {
                        ControlType = ControlTypes.Text,
                        SpeaksOwnPosition = true, // bare cells, no counts (user rule)
                        Announcements = new[]
                        {
                            new NodeAnnouncement(() => PixelText(number, cell), live: true,
                                kind: AnnouncementKinds.Label),
                        },
                        OnActivate = () => TogglePixel(number, cell),
                    });
                }
                b.EndRow();
            }
            b.PopContext();
        }

        private string PixelText(int number, int cell)
        {
            try
            {
                var e = Editor;
                var program = SolutionExaOf(number);
                if (e == null || program == null) return null;
                bool on;
                if (Editing(e))
                {
                    on = program.bool_0[cell];
                }
                else
                {
                    var live = FollowedExa(program);
                    if (live == null) return null;
                    on = live.bool_4[cell];
                }
                return Loc.T(on ? "editor.pixel.on" : "editor.pixel.off");
            }
            catch { return null; }
        }

        private void TogglePixel(int number, int cell)
        {
            var e = Editor;
            var program = SolutionExaOf(number);
            if (e == null || program == null) return;
            if (!Editing(e))
            {
                // The armed grid is a readout — the game wires its paint callback only at
                // edit time, so the mouse can't paint here either.
                Speech.Tts.Speak(Loc.T("value.unavailable"), interrupt: true);
                return;
            }
            try
            {
                // The game's click, in its order: method_33 (undo boundary) at the gesture,
                // then the pixel, then method_32 (dirty) — Class120.method_3's path.
                Invoke(SnapshotMethod, e);
                program.bool_0[cell] = !program.bool_0[cell];
                Invoke(DirtyMethod, e);
                // No explicit speech: the cell's Live label announces the flip.
            }
            catch (Exception ex) { Log.Error("[editor] sprite toggle failed", ex); }
        }
    }
}
