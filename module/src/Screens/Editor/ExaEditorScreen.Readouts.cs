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

        internal static string FileId(SimFile file)
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
        internal static bool HostHidden(SimHost host, bool goal)
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

        // The DISPLAYED host name: the game's name-plate draw has NO hidden gate
        // (EditorScreen's plate loop letters every host with a plate enum or name-mode strip,
        // covered or not — PB020's disc draws "LOCKED" across the cover AND "DISC" on the edge
        // frame; audited 2026-08-29), so a covered host speaks its drawn name when one is
        // drawn, falling back to the cover caption when nothing letters it (the cover replaces
        // CONTENTS only — occupants/files/registers stay gated elsewhere). A plateless host
        // (genum154_0 == 0 — the secret/modem boxes) shows no name at all, so the internal one
        // must never leak; then the uppercased internal name, overridable by the game mode
        // (the tutorial's home host is internally "player" but displays "RHIZOME"), then the
        // map's #-suffix truncation.
        internal static string HostName(SimHost host) => HostName(host, false);

        private static string HostName(SimHost host, bool goal)
        {
            if (host == null) return null;
            try
            {
                string drawn = DrawnHostName(host, goal);
                if (HostHidden(host, goal)) return drawn ?? LockedLabel(host);
                return drawn ?? UnnamedLabel(host);
            }
            catch { return null; }
        }

        /// <summary>The label for a host the map letters nothing for. When a map has SEVERAL
        /// such hosts, a sighted player still tells them apart by position — a link visibly
        /// runs to THAT host — so the label carries the host's hosts-stop position ("Unnamed
        /// host 4" = the 4th host row, the same number the navigator already speaks there);
        /// nothing is invented. A map's single unnamed host stays bare (PB023 audit,
        /// 2026-08-30: four identical labels made its one-way ring unfollowable).</summary>
        private static string UnnamedLabel(SimHost host)
        {
            try
            {
                var sim = TheSim(Editor);
                if (sim != null)
                {
                    int unnamed = 0, position = 0;
                    for (int i = 0; i < sim.list_0.Count; i++)
                    {
                        var h = sim.list_0[i];
                        if (ReferenceEquals(h, host)) position = i + 1;
                        if (!HostHidden(h, false) && DrawnHostName(h, false) == null) unnamed++;
                    }
                    if (unnamed > 1 && position > 0)
                        return Loc.T("editor.host.unnamed.n", new { n = position });
                }
            }
            catch { }
            return Loc.T("editor.host.unnamed");
        }

        /// <summary>The cover word as a hosts-stop VALUE — spoken after the drawn name
        /// ("DISC, Locked"); null when the caption already serves as the label (no name drawn)
        /// or the host isn't covered.</summary>
        internal static string HiddenStateValue(SimHost host)
        {
            try
            {
                if (!HostHidden(host, false)) return null;
                return DrawnHostName(host, false) != null ? LockedLabel(host) : null;
            }
            catch { return null; }
        }

        /// <summary>The name the map actually letters for this host, or null when nothing is
        /// drawn — mirrors EditorScreen's plate draw (which never gates on hidden), the
        /// name-display-mode strip scan, the home/opponent identity substitution, the logic's
        /// vmethod_10 override, and the #-suffix truncation.</summary>
        private static string DrawnHostName(SimHost host, bool goal)
        {
            try
            {
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
                            : null;
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
                    return null;
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
                // SANDBOX windows grow the G row (GX,GY,GZ sprite position) and the C row
                // (CO over CI) — live while armed; while editing the game draws the literal
                // placeholders "0,0,0" and -9999/-9999, mirrored exactly.
                if (SandboxMode(Editor))
                {
                    string g = "0,0,0", co = "-9999", ci = "-9999";
                    if (running)
                    {
                        g = exa.int_4 + "," + exa.int_5 + "," + exa.int_6;
                        co = exa.int_7.ToString();
                        ci = exa.int_8.ToString();
                    }
                    readout += " " + Loc.T("editor.exa.readout.gc", new { g, co, ci });
                }
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

        /// <summary>A file's AUTHORED row width — SimFile.int_0, set by the puzzles' fluent
        /// method_10 chain and drawn by the window as a FORCED line break after every Nth
        /// value (method_43's i % int_7): phone numbers, table rows, song pairs. Sighted
        /// players read that structure off the wrap; it is real model data, so every spoken
        /// surface mirrors it (user rule 2026-08-29). 0 = no structure — the window's
        /// char-width wrap is then purely presentational and stays unspoken.</summary>
        internal static int FileColumns(SimFile file)
        {
            try { return Math.Max(0, file.int_0); } catch { return 0; }
        }

        /// <summary>A required spec's row width: the SimFile that BACKS it carries the
        /// columns (specs have none of their own) — the live file it shadows when ids
        /// collide, else the goal ghost in sim.list_6 — the same precedence the F1 view
        /// draws with (method_42 reads the drawn window's own int_0).</summary>
        private static int RequiredFileColumns(Sim sim, SimRequiredFile spec)
        {
            try
            {
                foreach (var entity in sim.list_1)
                {
                    var f = entity as SimFile;
                    if (f != null && f.vmethod_1() == spec.entityID_0) return FileColumns(f);
                }
                foreach (var entity in sim.list_6)
                {
                    var f = entity as SimFile;
                    if (f != null && f.vmethod_1() == spec.entityID_0) return FileColumns(f);
                }
            }
            catch { }
            return 0;
        }

        /// <summary>A single value as spoken in comma joins: an EMPTY string value (the
        /// game draws its underline styling with no glyphs — the rating files' empty star
        /// slots) speaks "blank" so consecutive empties stay countable by ear — the
        /// sign-cell precedent for drawn-but-empty slots. Everything else passes through
        /// raw (user rule). Not applied to prose flow.</summary>
        private static string ValueSpeech(string t)
            => string.IsNullOrWhiteSpace(t) ? Loc.T("text.blank") : t;

        /// <summary>True when a value list is tokenized PROSE — it contains the game's "¶"
        /// paragraph-mark value (the books and articles ship as word/punctuation tokens).
        /// The drawn window shows the same comma-separated token stream, so flow-joining is
        /// TTS separator massaging of identical content (the TSpeech precedent), triggered
        /// only by the model's own markers, never by guessing.</summary>
        private static bool IsProse(System.Collections.Generic.IList<ExaValue> vals)
        {
            try
            {
                for (int i = 0; i < vals.Count; i++)
                    if (vals[i].method_2(true) == "¶") return true;
            }
            catch { }
            return false;
        }

        /// <summary>Tokenized prose as flowing speech: words joined by spaces, punctuation
        /// tokens attached to the word before them ("IS , WHY" → "IS, WHY"), "¶" spoken as
        /// the "; " pause. The characters are exactly the file's own values.</summary>
        private static string JoinProse(System.Collections.Generic.List<string> parts)
        {
            var sb = new System.Text.StringBuilder();
            bool start = true;
            foreach (var t in parts)
            {
                if (t == "¶")
                {
                    if (!start) sb.Append("; ");
                    start = true;
                    continue;
                }
                bool punct = t.Length > 0;
                foreach (var c in t)
                    if (char.IsLetterOrDigit(c)) { punct = false; break; }
                if (start) { sb.Append(t); start = false; }
                else if (punct) sb.Append(t);
                else sb.Append(' ').Append(t);
            }
            return sb.ToString().TrimEnd(' ', ';');
        }

        /// <summary>A prose file's paragraphs, each already flow-joined — the values popup's
        /// items ("¶" splits; empty segments are skipped).</summary>
        private static System.Collections.Generic.List<string> ProseSegments(
            System.Collections.Generic.IList<ExaValue> vals)
        {
            var segs = new System.Collections.Generic.List<string>();
            var cur = new System.Collections.Generic.List<string>();
            try
            {
                for (int i = 0; i < vals.Count; i++)
                {
                    string t = vals[i].method_2(true);
                    if (t == "¶")
                    {
                        if (cur.Count > 0) segs.Add(JoinProse(cur));
                        cur.Clear();
                    }
                    else cur.Add(t);
                }
                if (cur.Count > 0) segs.Add(JoinProse(cur));
            }
            catch { }
            return segs;
        }

        /// <summary>The spoken form of a (possibly capped) value list: flat comma join, or —
        /// when the file carries an authored row width — rows of that many values separated
        /// by "; ", the audible form of the drawn line break; or flowing prose when the list
        /// carries "¶" markers and no authored width. Appends "and N more" when the list was
        /// capped.</summary>
        private static string JoinValueRows(System.Collections.Generic.List<string> parts, int total, int cols, bool prose = false)
        {
            if (prose && cols <= 0)
            {
                string flow = JoinProse(parts);
                if (total > parts.Count)
                    flow += " " + Loc.T("editor.file.more", new { n = total - parts.Count });
                return flow;
            }
            // A capped list trims to WHOLE rows ("five numbers... and 33 more" beats a cut
            // mid-number); an uncapped list is never trimmed, and a row wider than the cap
            // keeps its partial row rather than vanishing.
            if (cols > 0 && total > parts.Count && parts.Count > cols)
                parts = parts.GetRange(0, parts.Count - parts.Count % cols);
            string joined;
            if (cols > 0 && cols < parts.Count)
            {
                var rows = new System.Collections.Generic.List<string>();
                for (int i = 0; i < parts.Count; i += cols)
                    rows.Add(string.Join(", ", parts.GetRange(i, Math.Min(cols, parts.Count - i))));
                joined = string.Join("; ", rows);
            }
            else joined = string.Join(", ", parts);
            if (total > parts.Count)
                joined += " " + Loc.T("editor.file.more", new { n = total - parts.Count });
            return joined;
        }

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
        /// player's team (EditorScreen's explicit team gate) — an off-team EXA is an anonymous
        /// sprite, so its internal name must never leak into speech. The anonymous label is
        /// MODE-AWARE (user rule 2026-08-29): "enemy EXA" fits a battle opponent, but a normal
        /// puzzle's off-team EXA is a scripted NPC (a librarian, a terminal), not an enemy —
        /// it speaks the neutral label. (Internal: the execution log's effect rows name actors
        /// through the same gate.)</summary>
        internal static string ExaDisplayName(SimExa exa)
        {
            try
            {
                var e = Editor;
                if (e != null && exa.team_0 != e.method_24())
                    return Loc.T(BattleMode(e) ? "editor.exa.enemy" : "editor.exa.other");
            }
            catch { }
            try { return exa.string_0; }
            catch { return null; }
        }

        /// <summary>A file in an OFF-TEAM EXA's hand is anonymous everywhere the game draws:
        /// the holder's card carries no id plate (the plate loop walks unheld drawables only),
        /// no window opens for it, and the goal view reuses that same window set — so neither
        /// its id nor its values are on screen for anyone until it drops. Surfaces that would
        /// name it (files stop, goal popup) skip it; the card itself speaks through the hosts
        /// row's "holding a file" (PB024's scripted players, 2026-08-30).</summary>
        private static bool HeldByOffTeam(SimFile file, SimExa holder, EditorScreen e)
        {
            try { return file != null && holder != null && e != null && holder.team_0 != e.method_24(); }
            catch { return false; }
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
                bool prose = IsProse(file.list_0);
                for (int i = 0; i < count && i < FileValuesSpoken; i++)
                {
                    string t = file.list_0[i].method_2(true);
                    parts.Add(prose ? t : ValueSpeech(t));
                }
                string values = JoinValueRows(parts, count, FileColumns(file), prose);
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
