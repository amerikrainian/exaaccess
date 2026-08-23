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

        // ---- run to caret line: the keyboard shape of the game's Alt+Click "run to
        // instruction". F8 arms the game's own marker (method_52, which does the
        // written->expanded remap while editing) at the caret's line — the game then runs and
        // pauses when ANY EXA of this program reaches it; Shift+F8 pins the SPECIFIC EXA. The
        // arrival pause keeps the sim ARMED with a zero step budget, which the stop narration
        // ignores — the OnUpdate watch below speaks the landing and arms the step echo. ----

        private int _runToLine;              // 1-based, for speech; 0 = idle
        private bool _suppressRunAnnounce;   // the arm announce replaces the generic "Running."

        private void RunToCaret(bool specificExa)
        {
            var e = Editor;
            if (e == null) return;
            var exa = FocusedCodeExa(e);
            if (exa == null || !Navigation.CaretTextEntryFocused)
            {
                Speech.Tts.Speak(Loc.T("value.unavailable"), interrupt: true);
                return;
            }
            string exaName; CompileError error;
            if (FirstCompileError(e, expanded: true, out exaName, out error))
            {
                // Same guard as Run: the game would flip the LOCKED error view and drop the
                // marker without a word.
                SpeakCompileError(exaName, error);
                return;
            }
            try
            {
                var pass = exa.gclass276_0.gclass286_1;
                int line, expanded;
                if (!Editing(e))
                {
                    // Armed: the target is the VIRTUAL read cursor's line — it already indexes
                    // the executing (expanded) listing, which is what method_52 takes mid-run.
                    if (_virtLine < 0)
                    {
                        Speech.Tts.Speak(Loc.T("value.unavailable"), interrupt: true);
                        return;
                    }
                    line = expanded = _virtLine;
                }
                else
                {
                    string text = exa.string_1 ?? string.Empty;
                    int caret = (int)CaretField.GetValue(exa.codeEditorWidget_0);
                    if (caret > text.Length) caret = text.Length;
                    line = CaretText.LineIndex(text, caret);
                    if (!pass.dictionary_0.TryGetValue(line, out expanded)) expanded = line;
                }
                // Eligibility mirrors the game's hover filter: real opcodes only (blank, NOTE
                // and MARK lines all compile to EmptyLine; a marker there would hunt forever).
                if (expanded >= pass.list_0.Count || !Sim.smethod_13(pass.list_0[expanded]))
                {
                    Speech.Tts.Speak(Loc.T("editor.runto.invalid"), interrupt: true);
                    return;
                }
                Invoke(PreActionMethod, e); // also clears any stale marker
                e.method_52((GEnum175)(specificExa ? 0 : 1), exa, EntityID.Exa(exa.method_0()),
                    Editing(e) ? line : expanded);
                _runToLine = line + 1;
                _suppressRunAnnounce = true;
                _stepEcho = false;
                Speech.Tts.Speak(Loc.T("editor.runto", new { line = line + 1 }), interrupt: true);
            }
            catch (Exception ex) { Log.Error("[editor] run-to failed", ex); }
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
            // The run-start confirmation comes from the state transition in OnUpdate — one
            // announcement whether the run began from our buttons or the native F4/F5.
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

        // "1 / 5" — current EXA count against the effective cap (the same two limits the game's
        // Create button greys out on: the per-team create cap and the storage limit).
        private static string ExaCountText()
        {
            try
            {
                var e = Editor;
                if (e == null) return null;
                int count = e.solution_0.list_0.Count;
                try
                {
                    var sim = TheSim(e);
                    if (sim != null)
                    {
                        int cap = Math.Min(sim.dictionary_0[e.method_24()].int_0, sim.int_6);
                        return count + " / " + cap;
                    }
                }
                catch { }
                return count.ToString();
            }
            catch { return null; }
        }
    }
}
