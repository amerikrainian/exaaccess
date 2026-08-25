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
        // ---- window-row actions (task 7) ----

        private static SolutionExa SolutionExaOf(int number)
        {
            try
            {
                var e = Editor;
                if (e == null) return null;
                foreach (var exa in e.solution_0.list_0)
                    if (exa.method_0() == number) return exa;
            }
            catch { }
            return null;
        }

        private static void JumpToCode(int number)
        {
            var e = Editor;
            var exa = SolutionExaOf(number);
            if (e == null || exa == null) return;
            ArmCodeFor(e, exa);
            Navigation.FocusStop("code");
        }

        /// <summary>Enter on a COPY's window row: its program's code, and while armed the
        /// read cursor lands on THE COPY's current instruction — the entry snap keeps it
        /// (it only re-snaps on a program change or an unset cursor).</summary>
        private void JumpToCopyCode(int entityNumber)
        {
            try
            {
                var exa = FindExaByEntity(entityNumber);
                if (exa == null || !exa.maybe_2.method_0()) return;
                int solution = exa.maybe_2.method_2().method_0();
                JumpToCode(solution);
                _followedEntity = entityNumber; // the code view now follows THIS instance
                int cl = CurrentListingLine(exa);
                if (!Editing(Editor) && cl >= 0)
                {
                    _virtExa = solution;
                    _virtLine = cl;
                }
            }
            catch (Exception ex) { Log.Error("[editor] copy code jump failed", ex); }
        }

        private static void DeleteExa(int number)
        {
            var e = Editor;
            var exa = SolutionExaOf(number);
            if (e == null || exa == null || !Editing(e)) return;
            try
            {
                string name = exa.string_0;
                e.method_36(exa); // remove + dirty + undo snapshot — Ctrl+Z restores
                Speech.Tts.Speak(Loc.T("editor.exa.deleted", new { exa = name }), interrupt: true);
            }
            catch (Exception ex) { Log.Error("[editor] delete EXA failed", ex); }
        }

        private static string MbusText(int number)
        {
            var exa = SolutionExaOf(number);
            if (exa == null) return null;
            try { return exa.mbusMode_0 == (MBusMode)1 ? GameText.T("Local") : GameText.T("Global"); }
            catch { return null; }
        }

        private static void ToggleMbus(int number)
        {
            var e = Editor;
            var exa = SolutionExaOf(number);
            if (e == null || exa == null || !Editing(e)) return;
            try
            {
                exa.mbusMode_0 = exa.mbusMode_0 == (MBusMode)1 ? (MBusMode)0 : (MBusMode)1;
                Invoke(DirtyMethod, e);
                Invoke(SnapshotMethod, e);
            }
            catch (Exception ex) { Log.Error("[editor] M-bus toggle failed", ex); }
        }

        private static SimExa FindExa(int number)
        {
            try
            {
                var e = Editor;
                var sim = TheSim(e);
                if (sim == null) return null;
                Team team = e.method_24();
                foreach (var entity in sim.list_1)
                {
                    var exa = entity as SimExa;
                    // Team-filtered: in battle the opponent's solution numbers its programs
                    // like ours — an unfiltered match could resolve to an ENEMY EXA.
                    if (exa != null && exa.team_0 == team && exa.maybe_2.method_0()
                        && exa.maybe_2.method_2().method_0() == number)
                        return exa;
                }
            }
            catch { }
            return null;
        }

        private static SimFile FindFile(string id)
        {
            try
            {
                var sim = TheSim(Editor);
                if (sim == null) return null;
                foreach (var entity in sim.list_1)
                {
                    var file = entity as SimFile;
                    if (file != null && FileId(file) == id) return file;
                }
            }
            catch { }
            return null;
        }

        private static string FileId(SimFile file)
        {
            // Battle maps carry PER-TEAM file ids (GStruct16: one id per side) — always read
            // the VIEWING player's view, the ids the map letters; the file's own team would
            // read the OPPONENT's ids on their movie files.
            try
            {
                var e = Editor;
                return file.vmethod_2(e != null ? e.method_24() : file.team_0);
            }
            catch { return null; }
        }

        /// <summary>host.method_10(goal) — true = drawn as a covered black box (contents,
        /// registers and files all suppressed on screen). The goal flag matters: some puzzles
        /// reveal hidden hosts under the F1 view only (SimHost.maybe_0).</summary>
        private static bool HostHidden(SimHost host, bool goal)
        {
            try { return host.method_10(goal); }
            catch { return false; }
        }

        /// <summary>The cover box's own caption when the puzzle set one ("Locked"), else our
        /// name for the caption-less cover art.</summary>
        private static string LockedLabel(SimHost host)
        {
            try
            {
                string caption = GameText.Speech(host.locString_0.ToString());
                if (!string.IsNullOrWhiteSpace(caption)) return caption;
            }
            catch { }
            return Loc.T("editor.host.locked");
        }

        // The DISPLAYED host name: a hidden host shows only its cover caption; a plateless
        // host (genum154_0 == 0 — the secret/modem boxes) shows no name at all, so the
        // internal one must never leak; then the uppercased internal name, overridable by the
        // game mode (the tutorial's home host is internally "player" but displays "RHIZOME"),
        // then the map's #-suffix truncation.
        private static string HostName(SimHost host) => HostName(host, false);

        private static string HostName(SimHost host, bool goal)
        {
            if (host == null) return null;
            try
            {
                if (HostHidden(host, goal)) return LockedLabel(host);
                var e = Editor;
                var meta = Meta(e);
                // NAME-DISPLAY MODE (meta.genum145_0 — the legacy-network puzzles): the game
                // skips plates for every host but YOUR home and letters the INTERNAL name
                // along a free 3-cell edge strip instead. Where no strip fits (single-cell
                // relays, edge-crowded hosts) nothing is drawn — unnamed for everyone.
                if (meta != null && meta.genum145_0 != (GEnum145)0 && e != null)
                {
                    var nsim = TheSim(e);
                    bool nameModeHome = false;
                    if (nsim != null)
                    {
                        Team homeTeam = e.method_24();
                        foreach (var pair in nsim.dictionary_0)
                            if (ReferenceEquals(pair.Value.simHost_0, host) && pair.Key == homeTeam)
                                nameModeHome = true;
                    }
                    if (!nameModeHome)
                        return nsim != null && NameLabelDrawn(host, nsim)
                            ? host.string_0.ToUpperInvariant()
                            : Loc.T("editor.host.unnamed");
                }
                if (host.genum154_0 == (GEnum154)0)
                {
                    // UC BERKELEY (the legacy-storage network): every visible name on this
                    // map is BAKED background art (Puzzles: one sim.method_34 overlay) — the
                    // banners transcribe the INTERNAL names (TAPE-1/2/3, EECS; file 300
                    // addresses hosts by exactly these strings), and the 1x1 relays are
                    // unlettered. The per-puzzle model read the sprite-content rule calls for.
                    var bsim = TheSim(e);
                    if (bsim != null && bsim.method_43() is SpecialPuzzleLogics.GClass304
                        && host.range2_0.Size.int_0 > 1 && host.range2_0.Size.int_1 > 1)
                        return host.string_0.ToUpperInvariant();
                    return Loc.T("editor.host.unnamed");
                }
                string name = null;
                // The two HOME hosts substitute by IDENTITY, exactly as the plate draw does
                // (EditorScreen's flag2/flag3, not the host's name): mine shows the player's
                // hostname — or the cover identity's handle ("UNKNOWN" for Moss) — except in
                // sandbox mode; the opponent's shows their handle.
                if (e != null && meta != null)
                {
                    Team mine = e.method_24();
                    bool myHome = false, theirHome = false;
                    var teams = TheSim(e);
                    if (teams != null)
                        foreach (var pair in teams.dictionary_0)
                        {
                            if (!ReferenceEquals(pair.Value.simHost_0, host)) continue;
                            if (pair.Key == mine) myHome = true;
                            else theirHome = true;
                        }
                    if (myHome && meta.genum18_0 != (GEnum18)2)
                        name = meta.bool_2
                            ? (meta.vignetteCharacter_1 == VignetteCharacter.Moss
                                ? "UNKNOWN" // the game's own literal for the covert identity
                                : Vignette.dictionary_0[meta.vignetteCharacter_1].Replace("\\", ""))
                            : GameLogic.gameLogic_0.saveData_0.method_32();
                    else if (theirHome)
                        name = OpponentName(e, meta);
                }
                if (name == null) name = host.string_0;
                name = name.ToUpperInvariant();
                var sim = TheSim(Editor);
                if (sim != null)
                {
                    var mode = sim.method_43();
                    var over = mode.vmethod_10(host, goal);
                    if (over.method_0()) name = over.method_2();
                }
                int hash = name.IndexOf('#');
                return hash >= 0 ? name.Substring(0, hash) : name;
            }
            catch { return null; }
        }

        private static readonly FieldInfo OpponentInfoField =
            Deobf.Field(typeof(EditorScreen), "multiplayerOpponentInfo_0");

        /// <summary>The opponent home host's plate: their Steam persona in a multiplayer battle
        /// (NetID converts implicitly to CSteamID), else the battle character's name.</summary>
        private static string OpponentName(EditorScreen e, GClass361 meta)
        {
            try
            {
                var mp = OpponentInfoField?.GetValue(e) as MultiplayerOpponentInfo;
                if (mp != null && mp.maybe_0.method_0())
                    return Steamworks.SteamFriends.GetFriendPersonaName(mp.maybe_0.method_2());
            }
            catch { }
            try { return Vignette.dictionary_0[meta.vignetteCharacter_0].Replace("\\", ""); }
            catch { return null; }
        }

        // "on RHIZOME. X 0, T 0, F none, M none global." — the register plate as one readout,
        // with the EXA's error line appended while it lasts (the sim keeps it ~one cycle).
        private static string ExaReadout(int number)
            => ExaReadoutOf(FindExa(number));

        /// <summary>A live EXA by its ENTITY number (EntityID.Exa) — unique even while REPL
        /// copies share their parent's SOLUTION number.</summary>
        private static SimExa FindExaByEntity(int entityNumber)
        {
            try
            {
                var sim = TheSim(Editor);
                if (sim == null) return null;
                foreach (var entity in sim.list_1)
                {
                    var exa = entity as SimExa;
                    if (exa != null && exa.entityID_0.Number == entityNumber) return exa;
                }
            }
            catch { }
            return null;
        }

        private static string ExaReadoutOf(SimExa exa)
        {
            if (exa == null) return null;
            try
            {
                string host = null;
                try { host = HostName(exa.method_0()); } catch { }
                string none = GameText.T("None");
                string x = exa.exaValue_0.method_2(true);
                string t = exa.exaValue_1.method_2(true);
                // A held file reads with its CURSOR: the value F would read/write next
                // ("200, cursor at 72"; "end" = the append position).
                string f = exa.maybe_3.method_0()
                    ? Loc.T("editor.f.held", new
                    {
                        id = FileId(exa.maybe_3.method_2()),
                        cursor = CursorValue(exa),
                    })
                    : none;
                string m = (exa.maybe_4.method_0() ? exa.maybe_4.method_2().method_2(true) : none)
                    + " " + (exa.mbusMode_0 == (MBusMode)1 ? GameText.T("Local") : GameText.T("Global"));
                string readout = Loc.T("editor.exa.readout",
                    new { host = host ?? "?", x, t, f, m });
                // Sim-only additions — the per-frame edit-time rebuild makes both meaningless
                // there (every EXA at line one, empty EXAs errored on every build).
                bool running = false;
                try { running = Editor?.method_0() == true; } catch { }
                if (running)
                {
                    // The window's highlighted line, bare, up front; an error's message (the
                    // game's own text, no added framing) splices in right behind the
                    // instruction that raised it ("LINK -1, Link ID not found.").
                    string line = PendingInstruction(exa);
                    string error = exa.bool_0 && !string.IsNullOrEmpty(exa.string_1)
                        ? GameText.Speech(exa.string_1)
                        : null;
                    string head = line == null ? error
                        : error == null ? line
                        : line + ", " + error;
                    if (head != null) readout = head + ". " + readout;
                }
                return readout;
            }
            catch { return null; }
        }

        /// <summary>The instruction an EXA executes next (the game's highlighted line), read from
        /// the macro-expanded source that actually runs; null when it has none to show.</summary>
        private static string PendingInstruction(SimExa exa)
        {
            try
            {
                var instr = exa.method_10();
                if (!instr.maybe_0.method_0()) return null;
                var lines = (exa.method_9() ?? string.Empty).Split('\n');
                int idx = instr.maybe_0.method_2();
                if (idx >= 0 && idx < lines.Length && lines[idx].Trim().Length > 0)
                    return lines[idx].Trim();
            }
            catch { }
            return null;
        }

        private const int FileValuesSpoken = 60;

        /// <summary>The value at an EXA's file cursor — what F reads/writes next; "end" when the
        /// cursor sits past the last value (a write appends, per the zine).</summary>
        private static string CursorValue(SimExa exa)
        {
            try
            {
                var file = exa.maybe_3.method_2();
                int cursor = exa.int_1;
                return cursor >= 0 && cursor < file.list_0.Count
                    ? file.list_0[cursor].method_2(true)
                    : Loc.T("editor.cursor.end");
            }
            catch { return null; }
        }

        /// <summary>An EXA's name as the map shows it: the game draws name tags ONLY for the
        /// player's team (EditorScreen's explicit team gate) — an opponent's EXA is an anonymous
        /// sprite, so its internal name must never leak into speech.</summary>
        private static string ExaDisplayName(SimExa exa)
        {
            try
            {
                var e = Editor;
                if (e != null && exa.team_0 != e.method_24()) return Loc.T("editor.exa.enemy");
            }
            catch { }
            try { return exa.string_0; }
            catch { return null; }
        }

        /// <summary>The player EXA currently holding this file, else null.</summary>
        private static SimExa HolderOf(SimFile file)
        {
            try
            {
                var sim = TheSim(Editor);
                if (sim == null) return null;
                foreach (var entity in sim.list_1)
                {
                    var exa = entity as SimExa;
                    if (exa != null && exa.maybe_3.method_0()
                        && ReferenceEquals(exa.maybe_3.method_2(), file)) return exa;
                }
            }
            catch { }
            return null;
        }

        private static string FileReadout(string id)
            => FileReadoutOf(FindFile(id));

        /// <summary>File ids are only unique per host (the legacy-storage puzzles ship the
        /// same id in several hosts) — rows built for a host must re-find by id AND live
        /// location, or they read the FIRST host's copy of that id.</summary>
        private static string FileReadout(string id, int hostIndex)
            => FileReadoutOf(FindFileAt(id, hostIndex) ?? FindFile(id));

        private static SimFile FindFileAt(string id, int hostIndex)
        {
            try
            {
                var sim = TheSim(Editor);
                if (sim == null || hostIndex < 0 || hostIndex >= sim.list_0.Count) return null;
                var host = sim.list_0[hostIndex];
                foreach (var entity in sim.list_1)
                {
                    var file = entity as SimFile;
                    if (file == null || FileId(file) != id) continue;
                    var holder = HolderOf(file);
                    var at = holder != null ? holder.method_0() : file.method_0();
                    if (ReferenceEquals(at, host)) return file;
                }
            }
            catch { }
            return null;
        }

        /// <summary>The file behind an EDITOR WINDOW's EntityID: an immovable file is keyed
        /// id@hostname (the id alone repeats across hosts), a movable one by bare id.</summary>
        private static SimFile FindFileForWindow(int number, Maybe<string> hostname)
        {
            try
            {
                var sim = TheSim(Editor);
                if (sim == null) return null;
                string id = number.ToString();
                bool qualified = hostname.method_0();
                foreach (var entity in sim.list_1)
                {
                    var file = entity as SimFile;
                    if (file == null || FileId(file) != id) continue;
                    if (!qualified) return file;
                    var holder = HolderOf(file);
                    var at = holder != null ? holder.method_0() : file.method_0();
                    if (at != null && at.string_0 == hostname.method_2()) return file;
                }
            }
            catch { }
            return null;
        }

        /// <summary>Whether the map actually DRAWS this host's internal-name label in
        /// name-display mode: the label needs a 3-cell strip along one of the four edges
        /// free of every host cell and link endpoint — single-cell relays fail the size
        /// gate and edge-crowded hosts find no strip, and then the name is on screen for
        /// no one. Mirrors the EditorScreen plate draw (minus label-vs-label crowding
        /// between two eligible neighbors).</summary>
        private static bool NameLabelDrawn(SimHost host, Sim sim)
        {
            try
            {
                var occupied = new System.Collections.Generic.HashSet<Index2>();
                foreach (var h in sim.list_0)
                {
                    foreach (var cell in h.range2_0.Indexes) occupied.Add(cell);
                    foreach (var link in h.list_1)
                    {
                        occupied.Add(link.index2_0);
                        occupied.Add(link.index2_1);
                    }
                }
                var r = host.range2_0;
                for (int i = 0; i < 4; i++)
                {
                    Index2 start, dir;
                    switch (i)
                    {
                        case 0: start = r.Start + new Index2(0, -1); dir = new Index2(1, 0); break;
                        case 1: start = new Index2(r.Start.int_0, r.End.int_1); dir = new Index2(1, 0); break;
                        case 2: start = new Index2(r.End.int_0, r.Start.int_1); dir = new Index2(0, 1); break;
                        default: start = r.End + new Index2(0, -1); dir = new Index2(0, -1); break;
                    }
                    if ((dir.int_0 != 0 && r.Size.int_0 < 2) || (dir.int_1 != 0 && r.Size.int_1 < 2)) continue;
                    bool free = true;
                    for (int j = 0; j < 3 && free; j++) free = !occupied.Contains(start + dir * j);
                    if (free) return true;
                }
            }
            catch { }
            return false;
        }

        private static string FileReadoutOf(SimFile file)
        {
            if (file == null) return null;
            try
            {
                var parts = new System.Collections.Generic.List<string>();
                int count = file.list_0.Count;
                for (int i = 0; i < count && i < FileValuesSpoken; i++)
                    parts.Add(file.list_0[i].method_2(true));
                string values = string.Join(", ", parts);
                if (count > FileValuesSpoken)
                    values += " " + Loc.T("editor.file.more", new { n = count - FileValuesSpoken });
                // The host is the stop's context now; a HELD file reads with its holder and
                // cursor (the zine's file window attached beneath the EXA).
                var holder = HolderOf(file);
                // An ENEMY holder has no window, so its F cursor is never drawn — speak the
                // held state without it (your own holder's window shows the cursor).
                bool enemyHolder = false;
                try
                {
                    var he = Editor;
                    enemyHolder = holder != null && he != null && holder.team_0 != he.method_24();
                }
                catch { }
                string readout = holder == null
                    ? Loc.T("editor.file.readout", new { count, values })
                    : enemyHolder
                        ? Loc.T("editor.file.held.enemy", new
                        {
                            count,
                            exa = ExaDisplayName(holder),
                            values,
                        })
                        : Loc.T("editor.file.held", new
                        {
                            count,
                            exa = ExaDisplayName(holder),
                            cursor = CursorValue(holder),
                            values,
                        });
                // The widened plate + icon the map draws on files LINK refuses to carry.
                bool immovable = false;
                try { immovable = (file.genum142_0 & (GEnum142)2) != (GEnum142)2; } catch { }
                return immovable ? readout + ", " + Loc.T("editor.file.immovable") : readout;
            }
            catch { return null; }
        }
    }
}
