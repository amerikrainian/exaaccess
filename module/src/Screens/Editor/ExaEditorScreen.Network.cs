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
        // ---- network topology (task 6): a hosts stop (scroll over hosts, selection follows
        // focus) and a links stop listing the SELECTED host's outgoing links — "800, INBOX",
        // prefixed "One way" when the far side has no return id (the game's own marker: link ids
        // are drawn per side only where they exist). ----

        private int _selectedHost;

        private void BuildHosts(GraphBuilder b, EditorScreen e)
        {
            var sim = TheSim(e);
            if (sim == null || sim.list_0.Count == 0) return;
            b.BeginStop("hosts");
            b.PushContext(Loc.T("editor.hosts"));
            for (int i = 0; i < sim.list_0.Count; i++)
            {
                int index = i;
                b.AddItem(ControlId.Structural("ed.host." + i), new NodeVtable
                {
                    ControlType = ControlTypes.Text,
                    Announcements = new[]
                    {
                        new NodeAnnouncement(() => HostName(HostAt(index)), kind: AnnouncementKinds.Label),
                        new NodeAnnouncement(() => HostOccupants(index), kind: AnnouncementKinds.Value),
                    },
                    Selected = () => _selectedHost == index,
                    OnSelect = () => _selectedHost = index,
                });
            }
            b.PopContext();
        }

        private void BuildLinks(GraphBuilder b, EditorScreen e)
        {
            var sim = TheSim(e);
            if (sim == null || _selectedHost >= sim.list_0.Count) return;
            var host = sim.list_0[_selectedHost];
            Team team;
            try { team = e.method_24(); }
            catch { return; }

            b.BeginStop("links");
            b.PushContext(Loc.T("editor.links", new { host = HostName(host) }));
            for (int i = 0; i < host.list_1.Count; i++)
            {
                var link = host.list_1[i];
                Maybe<int> localId;
                try { localId = link.method_2(host).method_2(team); }
                catch { continue; }
                // A LOCKED link draws as a red line even with its ids cleared — a visible
                // "connection exists, currently closed". A linkless, unlocked side is nothing
                // from here when the FAR side carries the id (the one-way link is heard from
                // there) — but a connector blank at BOTH ends is drawn (a ramp with an empty
                // plate on each side) and would be heard from nowhere, so both sides speak
                // the empty plate as their id, the "blank" word empty drawn values already
                // use (PB028's terminal↔storage corridor, audit 2026-09-06).
                bool locked = false;
                try { locked = link.bool_0; } catch { }
                var dest = link.method_1(host);
                bool farHasId = false;
                try { farHasId = link.method_2(dest).method_2(team).method_0(); } catch { }
                bool bothBlank = !localId.method_0() && !farHasId && !locked;
                if (!localId.method_0() && !locked && !bothBlank) continue;
                // The flag's only DRAWN form is the red link tile of the flat-tile maps
                // (meta.bool_1 — EditorScreen's color2); on an art map a flagged link that
                // carries an id is lettered and traversed like any other (PB056's home link,
                // audit 2026-09-20), so "locked" there would describe nothing on screen. The
                // id-less flagged rows (the modems' undialed lines) keep the word.
                bool flatTiles = false;
                try { flatTiles = Meta(e).bool_1; } catch { }
                bool speakLocked = locked && (flatTiles || !localId.method_0());
                string idPart = localId.method_0() ? localId.method_2() + ", "
                    : bothBlank ? Loc.T("text.blank") + ", " : "";
                string other = HostName(dest);
                bool oneWay = localId.method_0() && !farHasId;
                int destIndex = sim.list_0.IndexOf(dest);
                b.AddItem(ControlId.Structural("ed.link." + _selectedHost + "." + i), new NodeVtable
                {
                    ControlType = ControlTypes.Text,
                    Announcements = new[]
                    {
                        new NodeAnnouncement(() => (oneWay ? Loc.T("editor.link.oneway") + ", " : "")
                            + idPart + other
                            + (speakLocked ? ", " + Loc.T("editor.link.locked") : ""),
                            kind: AnnouncementKinds.Label),
                    },
                    // Enter = traverse, "as if you scrolled to the destination": it becomes the
                    // selected host AND the hosts stop's remembered row (stop landings prefer
                    // memory, so Shift+Tab must land on it), then focus re-parks on ITS links —
                    // that landing is the one the differ announces.
                    OnActivate = () =>
                    {
                        if (destIndex < 0) return;
                        _selectedHost = destIndex;
                        Navigation.FocusNode(ControlId.Structural("ed.host." + destIndex), announce: false);
                        Navigation.FocusStop("links");
                    },
                });
            }
            b.PopContext();
        }

        private static SimHost HostAt(int index)
        {
            try
            {
                var sim = TheSim(Editor);
                return sim != null && index < sim.list_0.Count ? sim.list_0[index] : null;
            }
            catch { return null; }
        }

        // Terse occupancy with capacity: "1 / 9, XA" — occupied cells against the host's usable
        // cells (a full host blocks entry; the game shows this only as the grid filling up).
        private static string HostOccupants(int index)
        {
            var host = HostAt(index);
            if (host == null) return null;
            // The cover box shows nothing inside — but its cover word ("LOCKED") is drawn, so
            // when the label carries the drawn name the caption speaks as the row's value.
            if (HostHidden(host, false)) return HiddenStateValue(host);
            try
            {
                var parts = new System.Collections.Generic.List<string>();
                foreach (var entity in host.method_8())
                {
                    var exa = entity as SimExa;
                    if (exa != null)
                    {
                        // A holder is drawn WITH its file (the card in its hand) while the file
                        // itself leaves this list (held = not in method_8) — say so, or the card
                        // a sighted player sees on every scripted NPC is silent (PB024, 2026-08-30).
                        bool holding = false;
                        try { holding = exa.maybe_3.method_0(); } catch { }
                        parts.Add(holding
                            ? Loc.T("editor.exa.holding", new { exa = ExaDisplayName(exa) })
                            : ExaDisplayName(exa));
                        continue;
                    }
                    var file = entity as SimFile;
                    if (file != null) parts.Add(Loc.T("editor.file", new { id = FileId(file) }));
                }
                string capacity = null;
                try { capacity = parts.Count + " / " + host.method_0(); } catch { }
                // Hardware registers, after the count — their cells are already outside method_0's
                // capacity. Label first, the way the map stacks its badge over the plate ("CNS #NERV").
                foreach (var reg in host.list_2)
                {
                    string name = RegName(reg);
                    if (name != null) parts.Add(name);
                }
                if (parts.Count == 0) return capacity;
                return capacity == null
                    ? string.Join(", ", parts)
                    : capacity + ", " + string.Join(", ", parts);
            }
            catch { return null; }
        }

        /// <summary>A hardware register as spoken: the game's badge label when the puzzle set one,
        /// then the register name — "CNS #NERV". (Internal: the exec log's enemy-write rows
        /// name plates through it too.)</summary>
        internal static string RegName(GClass265 reg)
        {
            try
            {
                return reg.maybe_0.method_0()
                    ? reg.maybe_0.method_2() + " " + reg.string_0
                    : reg.string_0;
            }
            catch { return null; }
        }
    }
}
