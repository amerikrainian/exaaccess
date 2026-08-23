using System;
using System.Collections.Generic;
using ExaAccess.Game;
using ExaAccess.Localization;
using HarmonyLib;

namespace ExaAccess.Patches
{
    /// <summary>
    /// Captures sim events the model destroys too fast to poll: an errored EXA keeps its message
    /// for roughly ONE cycle before the sim deletes it (and at fast-forward the whole run passes
    /// in a blink). A postfix on the error seam — Sim.smethod_16(SimExa, isError, message), the
    /// single chokepoint every runtime error and kill goes through — buffers player-EXA errors as
    /// composed announcements; the editor screen drains the queue each frame. The typed line
    /// number is recovered by inverting the compiler's written→expanded line map (identity for
    /// macro-free code).
    /// </summary>
    internal static class SimNarration
    {
        private static readonly Queue<string> Events = new Queue<string>();
        private static readonly object Gate = new object();

        // ---- which test run the current run STARTED on (0-based; -1 = none). A passing run
        // AUTO-ADVANCES through the validation tests, so an error can land on a DIFFERENT
        // random layout than the one the user was looking at — the error then names its test
        // ("Test 2: …"), and only then: failing the test you started on stays unprefixed
        // (user rule, 2026-08-23). The editor's run-state watch marks start/stop. ----
        private static readonly System.Reflection.MethodInfo RunNumber =
            Deobf.Method(typeof(EditorScreen), "method_23"); // the ACTIVE run while armed
        private static int _runStartTest = -1;

        public static void MarkRunStart(EditorScreen e) { _runStartTest = CurrentTest(e); }
        public static void ClearRunStart() { _runStartTest = -1; }

        private static int CurrentTest(EditorScreen e)
        {
            try { return e != null && RunNumber != null ? (int)RunNumber.Invoke(e, null) : -1; }
            catch { return -1; }
        }

        public static void Apply(Harmony harmony)
        {
            try
            {
                harmony.Patch(Expr.MethodOf(() => Sim.smethod_16(null, false, null)),
                    prefix: new HarmonyMethod(typeof(SimNarration), nameof(ErrorPrefix)),
                    postfix: new HarmonyMethod(typeof(SimNarration), nameof(ErrorPostfix)));
                Log.Info("[patch] sim narration armed");
            }
            catch (Exception ex) { Log.Error("[patch] sim narration failed to apply", ex); }
        }

        public static bool TryDequeue(out string message)
        {
            lock (Gate)
            {
                if (Events.Count > 0) { message = Events.Dequeue(); return true; }
                message = null;
                return false;
            }
        }

        // The seam no-ops when the EXA is already errored — remember whether this call is the one
        // that actually sets the state, so repeats never re-announce.
        private static void ErrorPrefix(SimExa __0, ref bool __state)
        {
            try { __state = __0 != null && __0.bool_0; } catch { __state = true; }
        }

        private static void ErrorPostfix(SimExa __0, bool __1, string __2, bool __state)
        {
            try
            {
                if (__state || __0 == null || !__0.maybe_2.method_0()) return; // player EXAs, first set only
                // While EDITING the whole sim is rebuilt every frame and zero-instruction EXAs
                // re-error on every rebuild — only a genuinely RUNNING sim's errors are events.
                var editor = GameState.TopScreen() as EditorScreen;
                if (editor == null || !editor.method_0()) return;
                lock (Gate)
                {
                    if (Events.Count > 16) return; // safety cap
                    string line = WrittenLine(__0);
                    string msg = line != null
                        ? Loc.T("editor.error.line", new { exa = __0.string_0, line, message = GameText.Speech(__2) })
                        : Loc.T("editor.error", new { exa = __0.string_0, message = GameText.Speech(__2) });
                    int test = CurrentTest(editor);
                    if (_runStartTest >= 0 && test >= 0 && test != _runStartTest)
                        msg = Loc.T("editor.error.test", new { test = test + 1, message = msg });
                    Events.Enqueue(msg);
                }
            }
            catch { }
        }

        /// <summary>The 1-based line the user TYPED for the EXA's current instruction — the
        /// instruction indexes the macro-expanded pass; the compiler's line map inverts it.</summary>
        private static string WrittenLine(SimExa exa)
        {
            try
            {
                var instr = exa.method_10();
                if (!instr.maybe_0.method_0()) return null;
                int expanded = instr.maybe_0.method_2();
                var map = exa.maybe_2.method_2().gclass276_0.gclass286_1.dictionary_0;
                foreach (var pair in map)
                    if (pair.Value == expanded) return (pair.Key + 1).ToString();
                return (expanded + 1).ToString(); // identity fallback (macro-free code)
            }
            catch { return null; }
        }
    }
}
