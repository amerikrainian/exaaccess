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
        // ---- files, grouped by host like the links stop: the SELECTED host's files (a held
        // file counts as being where its holder is), label = the bare id, value = count +
        // values. Enter = the values popup. No stop at all when the host holds nothing. ----

        private void BuildFiles(GraphBuilder b, EditorScreen e)
        {
            var sim = TheSim(e);
            if (sim == null || _selectedHost >= sim.list_0.Count) return;
            var host = sim.list_0[_selectedHost];
            if (HostHidden(host, false)) return; // hidden host = contents off the map
            var ids = new System.Collections.Generic.List<string>();
            foreach (var entity in sim.list_1)
            {
                var file = entity as SimFile;
                if (file == null) continue;
                string fid = FileId(file);
                if (fid == null) continue;
                SimHost at = null;
                try
                {
                    var holder = HolderOf(file);
                    at = holder != null ? holder.method_0() : file.method_0();
                }
                catch { }
                if (ReferenceEquals(at, host)) ids.Add(fid);
            }
            if (ids.Count == 0) return; // no files here — no stop at all
            b.BeginStop("files");
            b.PushContext(Loc.T("editor.files", new { host = HostName(host) }));
            foreach (var id in ids)
            {
                string fid = id;
                b.AddItem(ControlId.Structural("ed.file." + fid), new NodeVtable
                {
                    ControlType = ControlTypes.Text,
                    Announcements = new[]
                    {
                        new NodeAnnouncement(() => fid, kind: AnnouncementKinds.Label),
                        new NodeAnnouncement(() => FileReadout(fid), kind: AnnouncementKinds.Value),
                    },
                    OnActivate = () => OpenFilePopup(fid),
                });
            }
            b.PopContext();
        }

        // ---- hardware registers, grouped by host like links and files: the SELECTED host's
        // registers, one terse row each — label + name, value = the LIVE plate read (the
        // sighted player's glance while stepping; goal-side values live in the goal popup;
        // write-only plates draw none, so their row is the name alone). No stop when the
        // selected host has no registers — the usual case. ----

        private void BuildRegisters(GraphBuilder b, EditorScreen e)
        {
            var sim = TheSim(e);
            if (sim == null || _selectedHost >= sim.list_0.Count) return;
            var host = sim.list_0[_selectedHost];
            if (host.list_2.Count == 0) return;
            if (HostHidden(host, false)) return; // hidden host = registers off the map
            b.BeginStop("registers");
            b.PushContext(Loc.T("editor.registers", new { host = HostName(host) }));
            for (int r = 0; r < host.list_2.Count; r++)
            {
                int hi = _selectedHost, ri = r;
                b.AddItem(ControlId.Structural("ed.reg." + hi + "." + r), new NodeVtable
                {
                    ControlType = ControlTypes.Text,
                    Announcements = new[]
                    {
                        new NodeAnnouncement(() => RegName(RegAt(hi, ri)), kind: AnnouncementKinds.Label),
                        new NodeAnnouncement(() => RegValue(hi, ri), kind: AnnouncementKinds.Value),
                    },
                });
            }
            b.PopContext();
        }

        /// <summary>Re-resolved per announce — the sim (and every register in it) is rebuilt each
        /// frame while editing; captured instances go stale.</summary>
        private static GClass265 RegAt(int hostIndex, int regIndex)
        {
            try
            {
                var sim = TheSim(Editor);
                if (sim == null || hostIndex >= sim.list_0.Count) return null;
                var regs = sim.list_0[hostIndex].list_2;
                return regIndex < regs.Count ? regs[regIndex] : null;
            }
            catch { return null; }
        }

        private static string RegValue(int hostIndex, int regIndex)
        {
            try
            {
                var e = Editor;
                var sim = TheSim(e);
                var reg = RegAt(hostIndex, regIndex);
                if (sim == null || reg == null) return null;
                // The map distinguishes the plates by texture: write-only gets its own art,
                // and only readable (0) plates draw a value.
                if (reg.genum160_0 == (GEnum160)1) return Loc.T("editor.reg.writeonly");
                if (reg.genum160_0 != (GEnum160)0) return null;
                return sim.method_43().vmethod_9(reg, false, e.method_24(), false).method_2(true);
            }
            catch { return null; }
        }
    }
}
