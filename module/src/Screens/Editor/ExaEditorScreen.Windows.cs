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
        // per file window (location + contents). The Sim is rebuilt every frame, so rows are
        // keyed by stable ids and every announcement re-finds its entity live. ----

        private static readonly FieldInfo WindowListField = Deobf.Field(typeof(EditorScreen), "dictionary_1");

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

            // FILE windows, after the EXAs like the drawn column: the game pre-opens a viewer
            // per goal-relevant file (the sighted player reads tape contents without touching
            // the map). Label = the drawn title (the bare id); value = where it sits + the
            // usual readout. Enter = the values popup on the file's CURRENT host.
            try
            {
                var windows = WindowListField?.GetValue(e)
                    as System.Collections.Generic.Dictionary<EntityID, EditorWindow>;
                if (windows != null)
                    foreach (var winId in windows.Keys)
                    {
                        if (winId.Type != (GEnum147)1) continue; // files only — EXAs handled above
                        var id = winId;
                        string key = "win.file." + id.Number + "."
                            + (id.Hostname.method_0() ? id.Hostname.method_2() : "-");
                        b.AddItem(ControlId.Structural(key), new NodeVtable
                        {
                            ControlType = ControlTypes.Text,
                            Announcements = new[]
                            {
                                new NodeAnnouncement(() => id.Number.ToString(), kind: AnnouncementKinds.Label),
                                new NodeAnnouncement(() => WindowFileReadout(id), kind: AnnouncementKinds.Value),
                            },
                            OnActivate = () => OpenWindowFilePopup(id),
                        });
                    }
            }
            catch (Exception ex) { Log.Error("[editor] file windows failed", ex); }
            b.PopContext();
        }

        /// <summary>"at {host}" + the standard file readout — the map shows the same: the
        /// file plate sits on its host's grid, HostName never leaks a hidden/undrawn name.</summary>
        private static string WindowFileReadout(EntityID id)
        {
            var file = FindFileForWindow(id.Number, id.Hostname);
            if (file == null) return null;
            string readout = FileReadoutOf(file);
            try
            {
                var holder = HolderOf(file);
                var at = holder != null ? holder.method_0() : file.method_0();
                string host = HostName(at);
                if (!string.IsNullOrEmpty(host))
                    return Loc.T("editor.window.file.at", new { host }) + ", " + readout;
            }
            catch { }
            return readout;
        }

        private void OpenWindowFilePopup(EntityID id)
        {
            try
            {
                var sim = TheSim(Editor);
                var file = FindFileForWindow(id.Number, id.Hostname);
                if (sim == null || file == null) return;
                var holder = HolderOf(file);
                var at = holder != null ? holder.method_0() : file.method_0();
                string key = "win.file." + id.Number + "."
                    + (id.Hostname.method_0() ? id.Hostname.method_2() : "-");
                OpenFilePopup(FileId(file), sim.list_0.IndexOf(at), ControlId.Structural(key));
            }
            catch (Exception ex) { Log.Error("[editor] file window popup failed", ex); }
        }
    }
}
