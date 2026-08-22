using System;
using System.Reflection;
using ExaAccess.Game;
using ExaAccess.Localization;
using ExaAccess.UI;
using ExaAccess.UI.Graph;

namespace ExaAccess.Screens
{
    /// <summary>
    /// The EXA editor (game type EditorScreen, name-preserved) — the puzzle-solving screen and the
    /// mod's long game (see "EXA editor decoded" in CLAUDE.md). TASK 1 of the build-out: the
    /// read-only browse skeleton — three Tab stops over the fully-text parts of the screen: the
    /// task panel (puzzle description + live goal checklist), the score readouts (test run,
    /// cycles/size/activity — each carrying the game's own explainer tooltip on Space), and the
    /// solution header. Values read live at announce time; nothing is marked live-watched (cycles
    /// change 50×/second while running — announce-on-focus only). Escape stays native (the game's
    /// own leave-screen/reset behavior).
    /// </summary>
    public sealed class ExaEditorScreen : Screen
    {
        public ExaEditorScreen() { Wrap = true; }

        public override string Key => "editor";
        public override bool IsActive() => GameState.TopScreen() is EditorScreen;
        public override object InitialFocusStop => "task";

        public override string ScreenName
        {
            get
            {
                // The puzzle's own title; the generic label if anything is missing.
                try
                {
                    string title = GameText.Speech(Meta(Editor)?.locString_0.ToString());
                    if (!string.IsNullOrEmpty(title)) return title;
                }
                catch { }
                return Loc.T("screen.EditorScreen");
            }
        }

        // The private half (EditorScreen is name-preserved — direct namemap hits):
        // sim_0 = the live sim (rebuilt every frame while editing), maybe_1 = the size score,
        // method_23 = the effective test-run number while running.
        private static readonly FieldInfo SimField = Deobf.Field(typeof(EditorScreen), "sim_0");
        private static readonly FieldInfo SizeField = Deobf.Field(typeof(EditorScreen), "maybe_1");
        private static readonly MethodInfo RunNumberMethod = Deobf.Method(typeof(EditorScreen), "method_23");

        // The sim-control seams (decompile-verified against the button handlers):
        // method_18(fast, Maybe<int> stepCount) = start/advance, method_19 = reset, method_20 =
        // pause, method_22 = advance to the next test run once solved, method_57 = the common
        // pre-action (dismisses inline fields), bool_12 = the show-compile-errors state,
        // hashSet_0 = pending animations (gates the test-run advance).
        private static readonly MethodInfo AdvanceMethod = Deobf.Method(typeof(EditorScreen), "method_18");
        private static readonly MethodInfo ResetMethod = Deobf.Method(typeof(EditorScreen), "method_19");
        private static readonly MethodInfo PauseMethod = Deobf.Method(typeof(EditorScreen), "method_20");
        private static readonly MethodInfo NextRunMethod = Deobf.Method(typeof(EditorScreen), "method_22");
        private static readonly MethodInfo PreActionMethod = Deobf.Method(typeof(EditorScreen), "method_57");
        private static readonly FieldInfo ErrorViewField = Deobf.Field(typeof(EditorScreen), "bool_12");
        private static readonly FieldInfo AnimationsField = Deobf.Field(typeof(EditorScreen), "hashSet_0");

        // Edit-side seams (task 7): method_32 = mark undo-dirty, method_33 = undo snapshot,
        // method_25 = rebuild the sim now, method_54 = close inline fields, bool_7 = the
        // solution-name field's active flag.
        private static readonly MethodInfo DirtyMethod = Deobf.Method(typeof(EditorScreen), "method_32");
        private static readonly MethodInfo SnapshotMethod = Deobf.Method(typeof(EditorScreen), "method_33");
        private static readonly MethodInfo RebuildMethod = Deobf.Method(typeof(EditorScreen), "method_25");
        private static readonly MethodInfo CloseFieldsMethod = Deobf.Method(typeof(EditorScreen), "method_54");
        private static readonly FieldInfo NameActiveField = Deobf.Field(typeof(EditorScreen), "bool_7");

        private static EditorScreen Editor => GameState.TopScreen() as EditorScreen;

        private static Sim TheSim(EditorScreen e)
        {
            try { return SimField?.GetValue(e) as Sim; }
            catch { return null; }
        }

        // Puzzle → metadata is an extension on the INTERNAL static class Puzzles — invisible to
        // the compiler, reached by reflection (name-preserved class, so Deobf resolves directly).
        private static readonly MethodInfo PuzzleMetaMethod =
            Deobf.Method(typeof(Puzzle).Assembly.GetType("Puzzles"), "smethod_2");

        /// <summary>The puzzle's metadata blob (title, description, size limit).</summary>
        private static GClass361 Meta(EditorScreen e)
        {
            try
            {
                if (e == null || PuzzleMetaMethod == null) return null;
                return PuzzleMetaMethod.Invoke(null, new object[] { e.solution_0.method_0() }) as GClass361;
            }
            catch { return null; }
        }

        private static bool Editing(EditorScreen e)
        {
            try { return e != null && e.method_2(); }
            catch { return true; }
        }

        public override void Build(GraphBuilder b)
        {
            var e = Editor;
            if (e == null) return;
            BuildTask(b, e);
            BuildWindows(b, e);
            BuildCode(b, e);
            BuildHosts(b, e);
            BuildLinks(b, e);
            BuildStats(b, e);
            BuildControls(b);
            BuildSolution(b, e);
        }

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
                if (!localId.method_0()) continue; // no id from this side = not traversable from here
                int id = localId.method_2();
                string other = HostName(link.method_1(host));
                bool oneWay;
                try { oneWay = !link.method_2(link.method_1(host)).method_2(team).method_0(); }
                catch { oneWay = false; }
                b.AddItem(ControlId.Structural("ed.link." + _selectedHost + "." + i), new NodeVtable
                {
                    ControlType = ControlTypes.Text,
                    Announcements = new[]
                    {
                        new NodeAnnouncement(() => (oneWay ? Loc.T("editor.link.oneway") + ", " : "")
                            + id + ", " + other, kind: AnnouncementKinds.Label),
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

        // Terse occupancy: "XA, file 200" — nothing when empty.
        private static string HostOccupants(int index)
        {
            var host = HostAt(index);
            if (host == null) return null;
            try
            {
                var parts = new System.Collections.Generic.List<string>();
                foreach (var entity in host.method_8())
                {
                    var exa = entity as SimExa;
                    if (exa != null) { parts.Add(exa.string_0); continue; }
                    var file = entity as SimFile;
                    if (file != null) parts.Add(Loc.T("editor.file", new { id = FileId(file) }));
                }
                return parts.Count == 0 ? null : string.Join(", ", parts);
            }
            catch { return null; }
        }

        // ---- the CODE stop: one caret-owning node mirroring the game's focused EXA editor.
        // Tab in = the game's real code focus arms and you are typing (arrows/Home/End/Enter/
        // Delete/clipboard all native); Tab/Shift+Tab out = disarmed, back to browsing. The
        // game's own Ctrl+Up/Down switches which EXA is focused; the node follows. ----

        private static readonly FieldInfo FocusField = Deobf.Field(typeof(EditorScreen), "maybe_3");
        private static readonly FieldInfo FocusQueueField = Deobf.Field(typeof(EditorScreen), "maybe_4");
        private static readonly MethodInfo FocusExaMethod = Deobf.Method(typeof(EditorScreen), "method_58");
        private static readonly FieldInfo CaretField = Deobf.Field(typeof(CodeEditorWidget), "int_1");

        /// <summary>The SolutionExa whose CODE the game currently focuses, else null.</summary>
        private static SolutionExa FocusedCodeExa(EditorScreen e)
        {
            return CodeExaFrom(e, FocusField);
        }

        /// <summary>The announce-time target: the applied focus, else the QUEUED one (focus changes
        /// apply a frame late), else the first EXA.</summary>
        private static SolutionExa TargetCodeExa(EditorScreen e)
        {
            return FocusedCodeExa(e) ?? CodeExaFrom(e, FocusQueueField) ?? FirstExa(e);
        }

        private static SolutionExa CodeExaFrom(EditorScreen e, FieldInfo field)
        {
            try
            {
                var focus = (Maybe<GStruct9>)field.GetValue(e);
                if (!focus.method_0() || focus.method_2().genum13_0 != (GEnum13)1) return null;
                var id = focus.method_2().entityID_0;
                foreach (var exa in e.solution_0.list_0)
                    if (EntityID.Exa(exa.method_0()) == id) return exa;
            }
            catch { }
            return null;
        }

        private void BuildCode(GraphBuilder b, EditorScreen e)
        {
            if (e.solution_0.list_0.Count == 0) return;
            b.BeginStop("code");
            b.AddItem(ControlId.Structural("ed.code"), new NodeVtable
            {
                ControlType = ControlTypes.TextField,
                Announcements = new[]
                {
                    new NodeAnnouncement(() =>
                    {
                        var exa = TargetCodeExa(Editor);
                        return exa == null ? null : Loc.T("editor.code", new { exa = exa.string_0 });
                    }, kind: AnnouncementKinds.Label),
                    new NodeAnnouncement(() => CurrentLineText(), kind: AnnouncementKinds.Value),
                },
                TextEntry = true,
                TextEntryCaret = true,
                TextEchoCaps = false, // the widget uppercases every insert
                TextValue = () =>
                {
                    var exa = FocusedCodeExa(Editor);
                    return exa == null ? null : exa.string_1;
                },
                OnSelect = () => ArmCode(Editor),
            });
        }

        private static SolutionExa FirstExa(EditorScreen e)
        {
            try { return e != null && e.solution_0.list_0.Count > 0 ? e.solution_0.list_0[0] : null; }
            catch { return null; }
        }

        private static void ArmCode(EditorScreen e)
        {
            if (e == null || FocusedCodeExa(e) != null) return; // already armed
            ArmCodeFor(e, FirstExa(e));
        }

        private static void ArmCodeFor(EditorScreen e, SolutionExa exa)
        {
            if (e == null || exa == null) return;
            try
            {
                var id = EntityID.Exa(exa.method_0());
                // The game QUEUES focus changes (maybe_4, applied next frame); method_58 is only
                // the scroll-into-view half — both mirror the game's own Ctrl+Up/Down path.
                FocusQueueField?.SetValue(e, (Maybe<GStruct9>)new GStruct9(id, (GEnum13)1));
                FocusExaMethod?.Invoke(e, new object[] { id, true, true });
            }
            catch (Exception ex) { Log.Error("[editor] code arm failed", ex); }
        }

        private static void DisarmCode(EditorScreen e)
        {
            try { FocusField?.SetValue(e, (Maybe<GStruct9>)GStruct10.gstruct10_0); }
            catch (Exception ex) { Log.Error("[editor] code disarm failed", ex); }
        }

        // ---- caret narration (task 4B): while the code stop is focused, vertical caret moves
        // speak the line landed on, horizontal moves speak the character at the caret, and the
        // game's Ctrl+Up/Down EXA switch announces the new window. Text CHANGES are the typing
        // echo's job — they only re-baseline here, never double-speak. ----

        private int _caretExa = int.MinValue;
        private int _caretOffset = -1, _caretLine = -1;
        private string _caretText;

        private void NarrateCaret(EditorScreen e, bool baseline)
        {
            var exa = FocusedCodeExa(e);
            if (exa == null) return;
            int caret, exaNum;
            string text;
            try
            {
                caret = (int)CaretField.GetValue(exa.codeEditorWidget_0);
                text = exa.string_1 ?? string.Empty;
                exaNum = exa.method_0();
            }
            catch { return; }
            if (caret > text.Length) caret = text.Length;
            int line = LineIndex(text, caret);

            if (exaNum != _caretExa)
            {
                // First arm is silent (the landing announce covered it); a real EXA switch
                // (the game's Ctrl+Up/Down) announces the new window + its current line.
                if (!baseline && _caretExa != int.MinValue)
                    Speech.Tts.Speak(Loc.T("editor.code", new { exa = exa.string_0 })
                        + ", " + (CurrentLineText() ?? ""), interrupt: true);
            }
            else if (text != _caretText)
            {
                // An edit: the typing echo spoke it; just re-baseline below.
            }
            else if (line != _caretLine || caret != _caretOffset)
            {
                // Screen-reader convention (user rules, 2026-08-22): ONLY vertical moves read the
                // full line. Horizontal moves speak the character (a line boundary lands on the
                // newline and says just that); larger horizontal jumps (Ctrl word moves,
                // Home/End) speak the word landed on — even when they cross a line. The move
                // type comes from the actual key held, not from what the caret happened to do.
                bool vertical =
                    Input.SdlKeyboard.Held((int)Input.Scancode.Up)
                    || Input.SdlKeyboard.Held((int)Input.Scancode.Down)
                    || Input.SdlKeyboard.Held((int)Input.Scancode.PageUp)
                    || Input.SdlKeyboard.Held((int)Input.Scancode.PageDown)
                    || Input.SdlKeyboard.Held(96) || Input.SdlKeyboard.Held(90); // KP_8 / KP_2
                // Home/End are caret PLACEMENTS: they speak only the character landed on
                // (user rule, 2026-08-22) — only the Ctrl word jumps read whole words.
                bool homeEnd =
                    Input.SdlKeyboard.Held((int)Input.Scancode.Home)
                    || Input.SdlKeyboard.Held((int)Input.Scancode.End)
                    || Input.SdlKeyboard.Held(95) || Input.SdlKeyboard.Held(89); // KP_7 / KP_1
                if (vertical && line != _caretLine)
                    Speech.Tts.Speak(CurrentLineText(), interrupt: true);
                else if (homeEnd || Math.Abs(caret - _caretOffset) == 1)
                    Speech.Tts.Speak(CaretText.CharAt(text, caret), interrupt: true);
                else
                    Speech.Tts.Speak(CaretText.WordAt(text, caret), interrupt: true);
            }

            _caretExa = exaNum;
            _caretOffset = caret;
            _caretLine = line;
            _caretText = text;
        }

        private static int LineIndex(string text, int caret) => CaretText.LineIndex(text, caret);


        /// <summary>The text of the line the caret sits on ("blank" for an empty line).</summary>
        private static string CurrentLineText()
        {
            var e = Editor;
            var exa = TargetCodeExa(e); // arming is queued; read the target
            if (exa == null) return null;
            try
            {
                int caret = (int)CaretField.GetValue(exa.codeEditorWidget_0);
                string text = exa.string_1 ?? string.Empty;
                if (caret > text.Length) caret = text.Length;
                int start = text.LastIndexOf('\n', Math.Max(0, caret - 1)) + 1;
                int end = text.IndexOf('\n', caret);
                if (end < 0) end = text.Length;
                string line = start <= end ? text.Substring(start, end - start) : string.Empty;
                return line.Length == 0 ? Loc.T("text.blank") : line;
            }
            catch { return null; }
        }

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
            foreach (var entity in sim.list_1)
            {
                var file = entity as SimFile;
                if (file == null) continue;
                string id = FileId(file);
                if (id == null) continue;
                string fid = id;
                b.AddItem(ControlId.Structural("win.file." + fid), new NodeVtable
                {
                    ControlType = ControlTypes.Text,
                    Announcements = new[]
                    {
                        new NodeAnnouncement(() => Loc.T("editor.file", new { id = fid }), kind: AnnouncementKinds.Label),
                        new NodeAnnouncement(() => FileReadout(fid), kind: AnnouncementKinds.Value),
                    },
                });
            }
            b.PopContext();
        }

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
                var sim = TheSim(Editor);
                if (sim == null) return null;
                foreach (var entity in sim.list_1)
                {
                    var exa = entity as SimExa;
                    if (exa != null && exa.maybe_2.method_0() && exa.maybe_2.method_2().method_0() == number)
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
            try { return file.vmethod_2(file.team_0); }
            catch { return null; }
        }

        // The DISPLAYED host name: uppercased internal name, overridable by the game mode
        // (the tutorial's home host is internally "player" but displays "RHIZOME"), then the
        // map's #-suffix truncation.
        private static string HostName(SimHost host)
        {
            if (host == null) return null;
            try
            {
                string name = host.string_0.ToUpperInvariant();
                // The player's home host displays the HOSTNAME setting — or, in puzzles played
                // under a cover identity, that character's handle ("UNKNOWN" for Moss).
                if (name == "PLAYER")
                {
                    var meta = Meta(Editor);
                    if (meta != null && meta.bool_2)
                        name = meta.vignetteCharacter_1 == VignetteCharacter.Moss
                            ? "UNKNOWN"
                            : Vignette.dictionary_0[meta.vignetteCharacter_1].Replace("\\", "");
                    else
                        name = GameLogic.gameLogic_0.saveData_0.method_32();
                    name = name.ToUpperInvariant();
                }
                var sim = TheSim(Editor);
                if (sim != null)
                {
                    var mode = sim.method_43();
                    var over = mode.vmethod_10(host, false);
                    if (over.method_0()) name = over.method_2();
                }
                int hash = name.IndexOf('#');
                return hash >= 0 ? name.Substring(0, hash) : name;
            }
            catch { return null; }
        }

        // "on RHIZOME. X 0, T 0, F none, M none global." — the register plate as one readout,
        // with the EXA's error line appended while it lasts (the sim keeps it ~one cycle).
        private static string ExaReadout(int number)
        {
            var exa = FindExa(number);
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
                // The error line only means something while the sim RUNS — the per-frame edit-time
                // rebuild marks empty EXAs errored on every build.
                bool running = false;
                try { running = Editor?.method_0() == true; } catch { }
                if (running && exa.bool_0 && !string.IsNullOrEmpty(exa.string_1))
                    readout += " " + Loc.T("editor.exa.error", new { message = GameText.Speech(exa.string_1) });
                return readout;
            }
            catch { return null; }
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
        {
            var file = FindFile(id);
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
                // A HELD file reads with its holder and cursor instead of a host location —
                // the zine's file window attached beneath the EXA.
                var holder = HolderOf(file);
                if (holder != null)
                    return Loc.T("editor.file.held", new
                    {
                        count,
                        exa = holder.string_0,
                        cursor = CursorValue(holder),
                        values,
                    });
                string host = null;
                try { host = HostName(file.method_0()); } catch { }
                return Loc.T("editor.file.readout", new { count, host = host ?? "?", values });
            }
            catch { return null; }
        }

        /// <summary>F2 steps the sim from anywhere on this screen (the game's own Tab-step is
        /// suppressed so Tab stays stop-navigation — see CLAUDE.md).</summary>
        public override System.Collections.Generic.IEnumerable<ElementAction> GetActions()
        {
            yield return new ElementAction("ui.step", StepSim);
        }

        // ---- sim controls: the five buttons, each invoking the game's own handler path ----

        private void BuildControls(GraphBuilder b)
        {
            b.BeginStop("controls");
            b.PushContext(Loc.T("editor.controls"), positions: false);
            b.StartRow("ed.controls");
            ControlButton(b, "ed.ctl.step", "Step",
                "Press to advance the simulation a single cycle.\n\nHold to continuously advance the simulation.", StepSim);
            ControlButton(b, "ed.ctl.run", "Run",
                "Run the simulation at a speed that is slow enough to watch.", () => RunSim(fast: false));
            ControlButton(b, "ed.ctl.fast", "Fast-forward",
                "Run the simulation as quickly as possible.", () => RunSim(fast: true));
            ControlButton(b, "ed.ctl.pause", "Pause", "Pause the simulation.", PauseSim);
            ControlButton(b, "ed.ctl.reset", "Reset", "Reset the simulation.", ResetSim);
            b.EndRow();
            b.PopContext();
        }

        private static void ControlButton(GraphBuilder b, string id, string gameKey, string tooltipKey, Action activate)
        {
            b.AddItem(ControlId.Structural(id), new NodeVtable
            {
                ControlType = ControlTypes.Button,
                Announcements = new[]
                {
                    new NodeAnnouncement(() => GameText.T(gameKey), kind: AnnouncementKinds.Label),
                },
                OnTooltip = () => Speech.Tts.Speak(GameText.TSpeech(tooltipKey)),
                OnActivate = activate,
            });
        }

        // The first compile error across the solution, from the requested compile pass.
        private static bool FirstCompileError(EditorScreen e, bool expanded, out string exaName, out CompileError error)
        {
            exaName = null; error = null;
            try
            {
                foreach (var exa in e.solution_0.list_0)
                {
                    var errors = expanded ? exa.gclass276_0.gclass286_1.list_1 : exa.gclass276_0.gclass286_0.list_1;
                    if (errors.Count > 0)
                    {
                        exaName = exa.string_0;
                        error = errors[0];
                        return true;
                    }
                }
            }
            catch { }
            return false;
        }

        private static void SpeakCompileError(string exaName, CompileError error)
        {
            try
            {
                Speech.Tts.Speak(Loc.T("editor.compile_error",
                    new { exa = exaName, line = error.int_0 + 1, message = GameText.Speech(error.string_0) }));
            }
            catch { }
        }

        private static void Invoke(MethodInfo m, EditorScreen e, params object[] args)
        {
            try { m?.Invoke(e, args); }
            catch (Exception ex) { Log.Error("[editor] control invoke failed", ex); }
        }

        private bool _stepEcho; // announce cycle numbers while single-stepping (not while running)
        private int _lastCycle = -1;

        private void StepSim()
        {
            var e = Editor;
            if (e == null) return;
            Invoke(PreActionMethod, e);
            string exaName; CompileError error;
            if (FirstCompileError(e, expanded: true, out exaName, out error))
            {
                // The game's step-with-errors path flips into bool_12 — the read-only error VIEW,
                // which LOCKS the code editor until reset. Announcing the error carries the same
                // information without trapping a blind user out of their own code.
                SpeakCompileError(exaName, error);
                return;
            }
            var sim = TheSim(e);
            try
            {
                if (sim != null && sim.method_47())
                {
                    // Test run solved: step advances to the next run once animations settle.
                    var anims = AnimationsField?.GetValue(e) as System.Collections.ICollection;
                    if (anims == null || anims.Count == 0) Invoke(NextRunMethod, e);
                    return;
                }
            }
            catch { }
            _stepEcho = true;
            Invoke(AdvanceMethod, e, false, (Maybe<int>)(e.method_0() ? 1 : 0));
        }

        private void RunSim(bool fast)
        {
            var e = Editor;
            if (e == null) return;
            Invoke(PreActionMethod, e);
            string exaName; CompileError error;
            if (FirstCompileError(e, expanded: true, out exaName, out error))
            {
                // Announce only — never enter the editing-locked error view (see StepSim).
                SpeakCompileError(exaName, error);
                return;
            }
            _stepEcho = false;
            Invoke(AdvanceMethod, e, fast, (Maybe<int>)GStruct10.gstruct10_0);
            Speech.Tts.Speak(Loc.T(fast ? "editor.fast" : "editor.running"));
        }

        private void PauseSim()
        {
            var e = Editor;
            if (e == null || !e.method_0()) return;
            Invoke(PreActionMethod, e);
            Invoke(PauseMethod, e);
            _stepEcho = true; // stepping usually follows a pause
            Speech.Tts.Speak(Loc.T("editor.paused"));
        }

        private void ResetSim()
        {
            var e = Editor;
            if (e == null) return;
            Invoke(PreActionMethod, e);
            bool errorView = false;
            try { errorView = ErrorViewField != null && (bool)ErrorViewField.GetValue(e); } catch { }
            if (errorView)
            {
                try { ErrorViewField.SetValue(e, false); } catch { }
            }
            else if (e.method_0())
            {
                Invoke(ResetMethod, e);
            }
            _stepEcho = false;
            Speech.Tts.Speak(Loc.T("editor.reset"));
        }

        // ---- solution-name editing + Create New EXA (task 7) ----

        private static bool NameArmed(EditorScreen e)
        {
            try { return NameActiveField != null && (bool)NameActiveField.GetValue(e); }
            catch { return false; }
        }

        private static void ArmName(EditorScreen e)
        {
            if (e == null || !Editing(e) || NameArmed(e)) return;
            try
            {
                Invoke(CloseFieldsMethod, e);
                NameActiveField?.SetValue(e, true);
                GClass288.smethod_1(); // prime the text-input state, like the game's click
            }
            catch (Exception ex) { Log.Error("[editor] name arm failed", ex); }
        }

        private static void CommitName(EditorScreen e)
        {
            if (e == null || !NameArmed(e)) return;
            try
            {
                NameActiveField?.SetValue(e, false);
                Invoke(SnapshotMethod, e); // the game's own commit path (Enter/click-away)
            }
            catch (Exception ex) { Log.Error("[editor] name commit failed", ex); }
        }

        // Mirror of the Create New EXA handler (button / native Ctrl+Enter): create, snapshot,
        // rebuild, then focus the new EXA's code — landing our focus on the code stop with it.
        private void CreateExa()
        {
            var e = Editor;
            if (e == null || !Editing(e)) return;
            try
            {
                var sim = TheSim(e);
                if (sim != null && (sim.bool_4
                    || e.solution_0.list_0.Count >= sim.dictionary_0[e.method_24()].int_0
                    || e.solution_0.list_0.Count >= sim.int_6))
                {
                    Speech.Tts.Speak(Loc.T("value.unavailable"), interrupt: true);
                    return;
                }
                var id = e.solution_0.method_2();
                Invoke(DirtyMethod, e);
                Invoke(SnapshotMethod, e);
                Invoke(RebuildMethod, e);
                FocusQueueField?.SetValue(e, (Maybe<GStruct9>)new GStruct9(id, (GEnum13)1));
                FocusExaMethod?.Invoke(e, new object[] { id, true, true });
                Navigation.FocusStop("code");
            }
            catch (Exception ex) { Log.Error("[editor] create EXA failed", ex); }
        }

        // ---- per-frame: step-cycle echo, run-stop and test-run-complete announcements ----

        private object _instance;
        private bool _wasRunning, _wasSolved, _wasCodeFocused, _wasShowGoal;

        public override void OnUpdate()
        {
            var e = Editor;
            if (e == null) return;
            if (!ReferenceEquals(_instance, e))
            {
                _instance = e;
                _wasRunning = _wasSolved = _stepEcho = _wasCodeFocused = false;
                _lastCycle = -1;
            }

            // Leaving the code stop releases the game's real code focus (falling edge only, so a
            // mouse user's own click-focus is never fought over).
            bool codeFocused = Navigation.CaretTextEntryFocused;
            if (_wasCodeFocused && !codeFocused && FocusedCodeExa(e) != null) DisarmCode(e);
            if (codeFocused) NarrateCaret(e, !_wasCodeFocused);
            else _caretExa = int.MinValue;
            _wasCodeFocused = codeFocused;

            // Leaving the solution-name field commits it, like the game's click-away.
            if (NameArmed(e) && !Navigation.TextEntryFocused) CommitName(e);

            // The native F1 hold (Show Goal) speaks the goal details on press, same as our button.
            bool showGoal = false;
            try { showGoal = e.method_7(); } catch { }
            if (showGoal && !_wasShowGoal) SpeakGoalDetails();
            _wasShowGoal = showGoal;

            var sim = TheSim(e);
            bool running = false;
            try { running = e.method_0(); } catch { }

            // Stop transition FIRST: a reset rebuilds the sim at cycle 0, and the echo below
            // must not read that as a step.
            if (_wasRunning && !running)
            {
                _stepEcho = false;
                _lastCycle = -1;
                Speech.Tts.Speak(_runCycles > 0
                    ? Loc.T("editor.stopped.at", new { n = _runCycles })
                    : Loc.T("editor.stopped"));
                _runCycles = 0;
                _goalStates = null;
            }
            _wasRunning = running;

            int cycles = 0;
            try { cycles = sim != null ? sim.method_52() : 0; } catch { }
            if (running && cycles > 0) _runCycles = cycles;

            // Buffered sim events (errors captured by the SimNarration patch — the model deletes
            // errored EXAs after one cycle, so polling could never catch them).
            string ev;
            int drained = 0;
            while (drained++ < 4 && Patches.SimNarration.TryDequeue(out ev))
                Speech.Tts.Speak(ev);

            if (_stepEcho && sim != null && cycles != _lastCycle)
            {
                // Announce from cycle 0 too: the first step arms the sim paused, and hearing the
                // PENDING instruction ("Cycle 0. XA: LINK 800") is the point of stepping.
                if (_lastCycle >= 0 || cycles == 0)
                    Speech.Tts.Speak(StepNarration(e, cycles), interrupt: true);
                _lastCycle = cycles;
            }

            WatchGoals(e, sim, running, cycles);

            bool solved = false;
            try { solved = sim != null && sim.method_47(); } catch { }
            if (solved && !_wasSolved)
                Speech.Tts.Speak(GameText.T("Test Run Complete"));
            _wasSolved = solved;
        }

        private int _runCycles;
        private int[] _goalStates;

        // "Cycle 3. XA: LINK 800" — the next instruction of the code-focused EXA (or the first
        // live player EXA), read from the macro-expanded source that actually executes.
        private string StepNarration(EditorScreen e, int cycle)
        {
            string detail = null;
            try
            {
                var sim = TheSim(e);
                SimExa target = null;
                var focused = FocusedCodeExa(e);
                if (sim != null)
                    foreach (var entity in sim.list_1)
                    {
                        var exa = entity as SimExa;
                        if (exa == null || !exa.maybe_2.method_0()) continue;
                        if (focused != null && ReferenceEquals(exa.maybe_2.method_2(), focused)) { target = exa; break; }
                        if (target == null) target = exa;
                    }
                if (target != null)
                {
                    var instr = target.method_10();
                    if (instr.maybe_0.method_0())
                    {
                        var lines = (target.method_9() ?? string.Empty).Split('\n');
                        int idx = instr.maybe_0.method_2();
                        if (idx >= 0 && idx < lines.Length && lines[idx].Trim().Length > 0)
                            detail = target.string_0 + ": " + lines[idx].Trim();
                    }
                }
            }
            catch { }
            string cycleText = Loc.T("editor.cycle", new { n = cycle });
            return detail == null ? cycleText : cycleText + ". " + detail;
        }

        // Announce goal-state flips while the sim runs — the key feedback during Run/Fast.
        private void WatchGoals(EditorScreen e, Sim sim, bool running, int cycles)
        {
            if (sim == null || !running || cycles < 1) { _goalStates = null; return; }
            try
            {
                var goals = sim.list_2;
                if (_goalStates == null || _goalStates.Length != goals.Count)
                {
                    _goalStates = new int[goals.Count];
                    for (int i = 0; i < goals.Count; i++)
                        _goalStates[i] = (int)goals[i].imethod_1(sim, sim.list_5).genum152_0;
                    return;
                }
                for (int i = 0; i < goals.Count; i++)
                {
                    int state = (int)goals[i].imethod_1(sim, sim.list_5).genum152_0;
                    if (state != _goalStates[i] && state != 0)
                        Speech.Tts.Speak(GoalLabel(i) + ", "
                            + Loc.T(state == 1 ? "value.complete" : "value.failed"));
                    _goalStates[i] = state;
                }
            }
            catch { }
        }

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
            // The accessible Show Goal: reads the expected end-state (the required files the F1
            // view places on the map) as text instead of the alternate rendering.
            b.AddItem(ControlId.Structural("ed.goalbtn"), new NodeVtable
            {
                ControlType = ControlTypes.Button,
                Announcements = new[]
                {
                    new NodeAnnouncement(() => GameText.T("Show Goal"), kind: AnnouncementKinds.Label),
                },
                OnActivate = SpeakGoalDetails,
            });
            b.PopContext();
        }

        private static void SpeakGoalDetails()
        {
            try
            {
                var sim = TheSim(Editor);
                if (sim == null) return;
                var parts = new System.Collections.Generic.List<string>();
                foreach (var host in sim.list_0)
                    foreach (var required in host.list_0)
                    {
                        string id = required.maybe_0.method_0()
                            ? required.maybe_0.method_2().ToString() : GameText.T("NEW");
                        var values = new System.Collections.Generic.List<string>();
                        for (int i = 0; i < required.exaValue_0.Length && i < FileValuesSpoken; i++)
                            values.Add(required.exaValue_0[i].method_2(true));
                        parts.Add(Loc.T("editor.goal.file", new
                        {
                            id,
                            host = HostName(host),
                            values = string.Join(", ", values),
                        }));
                    }
                Speech.Tts.Speak(parts.Count == 0 ? Loc.T("nav.no_details") : string.Join(" ", parts));
            }
            catch { Speech.Tts.Speak(Loc.T("nav.no_details")); }
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

        // ---- the score readouts, each with the game's own explainer as its tooltip ----

        private void BuildStats(GraphBuilder b, EditorScreen e)
        {
            b.BeginStop("stats");
            b.PushContext(Loc.T("editor.stats"), positions: false);

            // The test-run row is ADJUSTABLE (Left/Right) while editing — the keyboard version of
            // the game's mouse-only arrows; while running it reads the current run and is inert.
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
                    ed.int_4 = Math.Max(0, Math.Min(99, ed.int_4 + sign * (large ? 10 : 1)));
                },
                OnTooltip = () => Speech.Tts.Speak(GameText.TSpeech("To complete a task you must complete all 100 test runs. You can change the initial test run to more easily debug problems that only occur on specific test runs.")),
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

        private static string RunText()
        {
            var ed = Editor;
            if (ed == null) return null;
            int run = ed.int_4;
            if (!Editing(ed))
                try { run = (int)RunNumberMethod.Invoke(ed, null); } catch { }
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
                    new NodeAnnouncement(() =>
                    {
                        try { return Editor?.solution_0.list_0.Count.ToString(); }
                        catch { return null; }
                    }, kind: AnnouncementKinds.Value),
                },
            });
            b.PopContext();
        }
    }

    /// <summary>The puzzle-completion screen (name-preserved), BROWSABLE (user rule): the final
    /// scores as arrow-navigable rows (no position counts) plus the two native options as rows
    /// naming their keys. Enter and Escape stay on the game's own paths — Enter (Return to
    /// Desktop) rides the suppression pass-through, Escape (Continue Editing) is never
    /// suppressed. Scores: size from the screen's field; cycles/activity as the maxima of the
    /// editor's public per-run table.</summary>
    public sealed class PuzzleCompleteScreen : Screen
    {
        public override string Key => "puzzle.complete";
        public override string ScreenName => Loc.T("screen.PuzzleCompletionScreen");

        public override bool IsActive() => GameState.TopScreen() is PuzzleCompletionScreen;

        // Enter must reach the game's Return to Desktop even while our graph browses.
        public override bool PassKeyToGame(int keycode) => keycode == 13 || keycode == 1073741912;

        private static readonly FieldInfo SizeField = Deobf.Field(typeof(PuzzleCompletionScreen), "int_0");
        private static readonly FieldInfo EditorField = Deobf.Field(typeof(PuzzleCompletionScreen), "editorScreen_0");

        public override void Build(GraphBuilder b)
        {
            var s = GameState.TopScreen() as PuzzleCompletionScreen;
            if (s == null) return;
            b.PushContext(Loc.T("editor.stats"), positions: false);
            StatRow(b, "pc.cycles", () => ScoreManager.locString_0.ToString(), () => Stat(s, 0));
            StatRow(b, "pc.size", () => ScoreManager.locString_1.ToString(), () => Stat(s, 1));
            StatRow(b, "pc.activity", () => ScoreManager.locString_2.ToString(), () => Stat(s, 2));
            b.PopContext();
        }

        // The options are announced once on entry (user rule) — the browsable rows are the stats.
        public override void OnFocus()
        {
            base.OnFocus();
            try
            {
                Speech.Tts.Speak(Loc.T("editor.complete.options", new
                {
                    resume = GameText.T("Continue Editing"),
                    leave = GameText.T("Return to Desktop"),
                }));
            }
            catch { }
        }

        private static void StatRow(GraphBuilder b, string id, Func<string> label, Func<string> value)
        {
            b.AddItem(ControlId.Structural(id), new NodeVtable
            {
                ControlType = ControlTypes.Text,
                SpeaksOwnPosition = true, // no "n of m" anywhere on this screen (user rule)
                Announcements = new[]
                {
                    new NodeAnnouncement(label, kind: AnnouncementKinds.Label),
                    new NodeAnnouncement(value, kind: AnnouncementKinds.Value),
                },
            });
        }

        private static string Stat(PuzzleCompletionScreen s, int which)
        {
            try
            {
                if (which == 1) return SizeField != null ? SizeField.GetValue(s).ToString() : null;
                int best = 0;
                var editor = EditorField?.GetValue(s) as EditorScreen;
                if (editor != null)
                    foreach (var pair in editor.dictionary_0.Values)
                    {
                        int v = which == 0 ? pair.Item1 : pair.Item2;
                        if (v > best) best = v;
                    }
                return best.ToString();
            }
            catch { return null; }
        }
    }
}
