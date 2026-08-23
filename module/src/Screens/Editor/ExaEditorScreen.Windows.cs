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
        // ---- the window column: one row per player EXA (registers + location + error) and one
        // per file (location + contents). The Sim is rebuilt every frame, so rows are keyed by
        // stable ids and every announcement re-finds its entity live. ----

        private void BuildWindows(GraphBuilder b, EditorScreen e)
        {
            var sim = TheSim(e);
            if (sim == null) return;
            b.BeginStop("windows");
            b.PushContext(Loc.T("editor.windows"));
            foreach (var entity in sim.list_1)
            {
                var exa = entity as SimExa;
                if (exa == null || !exa.maybe_2.method_0()) continue; // player-authored EXAs only
                int number = 0;
                try { number = exa.maybe_2.method_2().method_0(); } catch { }
                int n = number;
                b.AddItem(ControlId.Structural("win.exa." + n), new NodeVtable
                {
                    ControlType = ControlTypes.Text,
                    Announcements = new[]
                    {
                        new NodeAnnouncement(() => FindExa(n)?.string_0, kind: AnnouncementKinds.Label),
                        new NodeAnnouncement(() => ExaReadout(n), kind: AnnouncementKinds.Value),
                    },
                    // Enter = edit this EXA's code; Backspace = delete it (the game's own
                    // method_36, instantly undoable with the native Ctrl+Z).
                    OnActivate = () => JumpToCode(n),
                    OnSecondary = () => DeleteExa(n),
                });
                b.AddItem(ControlId.Structural("win.mbus." + n), new NodeVtable
                {
                    ControlType = ControlTypes.Toggle,
                    Announcements = new[]
                    {
                        new NodeAnnouncement(() => Loc.T("editor.mbus", new { exa = FindExa(n)?.string_0 }),
                            kind: AnnouncementKinds.Label),
                        new NodeAnnouncement(() => MbusText(n), kind: AnnouncementKinds.Value),
                    },
                    StateText = () => MbusText(n),
                    OnActivate = () => ToggleMbus(n),
                });
            }
            b.PopContext();
        }
    }
}
