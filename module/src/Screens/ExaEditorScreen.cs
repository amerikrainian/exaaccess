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
            BuildStats(b, e);
            BuildControls(b);
            BuildSolution(b, e);
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
            try
            {
                var focus = (Maybe<GStruct9>)FocusField.GetValue(e);
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
                        var exa = FocusedCodeExa(Editor) ?? FirstExa(Editor);
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
            if (e == null) return;
            try
            {
                if (FocusedCodeExa(e) != null) return; // already armed
                var exa = FirstExa(e);
                if (exa == null) return;
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

        /// <summary>The text of the line the caret sits on ("blank" for an empty line).</summary>
        private static string CurrentLineText()
        {
            var e = Editor;
            var exa = FocusedCodeExa(e) ?? FirstExa(e); // arming is queued; read the target
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
                string f = exa.maybe_3.method_0() ? FileId(exa.maybe_3.method_2()) : none;
                string m = (exa.maybe_4.method_0() ? exa.maybe_4.method_2().method_2(true) : none)
                    + " " + (exa.mbusMode_0 == (MBusMode)1 ? GameText.T("Local") : GameText.T("Global"));
                string readout = Loc.T("editor.exa.readout",
                    new { host = host ?? "?", x, t, f, m });
                if (exa.bool_0 && !string.IsNullOrEmpty(exa.string_1))
                    readout += " " + Loc.T("editor.exa.error", new { message = GameText.Speech(exa.string_1) });
                return readout;
            }
            catch { return null; }
        }

        private const int FileValuesSpoken = 60;

        private static string FileReadout(string id)
        {
            var file = FindFile(id);
            if (file == null) return null;
            try
            {
                string host = null;
                try { host = HostName(file.method_0()); } catch { }
                var parts = new System.Collections.Generic.List<string>();
                int count = file.list_0.Count;
                for (int i = 0; i < count && i < FileValuesSpoken; i++)
                    parts.Add(file.list_0[i].method_2(true));
                string values = string.Join(", ", parts);
                if (count > FileValuesSpoken)
                    values += " " + Loc.T("editor.file.more", new { n = count - FileValuesSpoken });
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
                // The game's step-with-errors path flips into the error view; announce the error.
                try { ErrorViewField?.SetValue(e, true); } catch { }
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
                try { ErrorViewField?.SetValue(e, true); } catch { }
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

        // ---- per-frame: step-cycle echo, run-stop and test-run-complete announcements ----

        private object _instance;
        private bool _wasRunning, _wasSolved, _wasCodeFocused;

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
            _wasCodeFocused = codeFocused;

            var sim = TheSim(e);
            bool running = false;
            try { running = e.method_0(); } catch { }

            // Stop transition FIRST: a reset rebuilds the sim at cycle 0, and the echo below
            // must not read that as a step.
            if (_wasRunning && !running)
            {
                _stepEcho = false;
                _lastCycle = -1;
                Speech.Tts.Speak(Loc.T("editor.stopped"));
            }
            _wasRunning = running;

            if (_stepEcho && sim != null)
            {
                int cycle = 0;
                try { cycle = sim.method_52(); } catch { }
                if (cycle != _lastCycle)
                {
                    if (_lastCycle >= 0)
                        Speech.Tts.Speak(Loc.T("editor.cycle", new { n = cycle }), interrupt: true);
                    _lastCycle = cycle;
                }
            }

            bool solved = false;
            try { solved = sim != null && sim.method_47(); } catch { }
            if (solved && !_wasSolved)
                Speech.Tts.Speak(GameText.T("Test Run Complete"));
            _wasSolved = solved;
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
            b.PopContext();
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

            StatRow(b, "ed.run", () => GameText.T("Test Run"), () =>
            {
                var ed = Editor;
                if (ed == null) return null;
                int run = ed.int_4;
                if (!Editing(ed))
                    try { run = (int)RunNumberMethod.Invoke(ed, null); } catch { }
                return (run + 1) + " / 100";
            }, () => GameText.TSpeech("To complete a task you must complete all 100 test runs. You can change the initial test run to more easily debug problems that only occur on specific test runs."));

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
            b.AddItem(ControlId.Structural("ed.solname"), new NodeVtable
            {
                ControlType = ControlTypes.Text,
                Announcements = new[]
                {
                    new NodeAnnouncement(() => Loc.T("editor.solution.name"), kind: AnnouncementKinds.Label),
                    new NodeAnnouncement(() =>
                    {
                        try { return Editor?.solution_0.string_0; }
                        catch { return null; }
                    }, kind: AnnouncementKinds.Value),
                },
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
}
