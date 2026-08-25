using System;
using System.Collections.Generic;
using ExaAccess.Game;
using ExaAccess.UI;
using HarmonyLib;

namespace ExaAccess.Patches
{
    /// <summary>
    /// Records every instruction the player's EXAs execute, cycle by cycle, into the editor's
    /// EXECUTION LOG stop. The seam is Sim.method_55 — the single per-EXA dispatch the cycle step
    /// (method_54) routes every normal-mode EXA through, once per cycle, with method_10() = the
    /// instruction being executed and method_52() = the cycle it executes on (the counter
    /// increments after the whole cycle, so the instruction the step echo announced pending at
    /// "Cycle n" lands in cycle n). A blocked instruction (M with no peer, LINK on a busy link)
    /// dispatches again next cycle and records again — the same line the sighted player watches
    /// stay highlighted. Special modes (link travel, dying — method_57) execute no instruction and
    /// record nothing. Enemy/NPC EXAs are filtered the way the game filters windows: no
    /// SolutionExa or another team = code that is not the player's to read.
    /// </summary>
    internal static class ExecutionCapture
    {
        /// <summary>Main-thread only (the sim steps, our tick, and the graph build all run on the
        /// game's update thread) — no locking. The editor screen clears it on each arming.</summary>
        public static readonly ExecutionLog Store = new ExecutionLog();

        public static void Apply(Harmony harmony)
        {
            try
            {
                harmony.Patch(Deobf.Method(typeof(Sim), "method_55"),
                    prefix: new HarmonyMethod(typeof(ExecutionCapture), nameof(ExecutePrefix)));
                Log.Info("[patch] execution capture armed");
            }
            catch (Exception ex) { Log.Error("[patch] execution capture failed to apply", ex); }
        }

        /// <summary>Fresh run: drop the log and the listing cache (a recompile makes new listing
        /// strings; stale ones must not accumulate across puzzles).</summary>
        public static void Clear()
        {
            // The insurance cap firing means someone left fast-forward running for ~25 minutes
            // straight — worth a log line for the "my early cycles are missing" report.
            if (Store.DroppedCycles > 0)
                Log.Info("[execlog] insurance cap shed " + Store.DroppedCycles + " oldest cycles last run");
            Store.Clear();
            _lines.Clear();
        }

        private static void ExecutePrefix(Sim __instance, SimExa __0)
        {
            try
            {
                if (__0 == null || !__0.maybe_2.method_0()) return; // player-authored EXAs only
                var editor = GameState.TopScreen() as EditorScreen;
                if (editor == null || !editor.method_0()) return; // a genuinely running editor sim
                try { if (__0.team_0 != editor.method_24()) return; } catch { return; } // never the opponent's code
                string instr = ListingLine(__0);
                if (instr == null) return;
                Store.Add(SimNarration.CurrentTest(editor), __instance.method_52(),
                    __0.string_0 + ": " + instr);
            }
            catch { }
        }

        // The executing listing (macro-expanded, one line per instruction) is a cached string on
        // the compile result — but splitting it per record would allocate on every instruction of
        // a fast-forwarded run. Split once per listing, keyed by the string's identity.
        private static readonly Dictionary<string, string[]> _lines =
            new Dictionary<string, string[]>(ReferenceComparer.Instance);

        private static string ListingLine(SimExa exa)
        {
            try
            {
                var instr = exa.method_10();
                if (!instr.maybe_0.method_0()) return null;
                string listing = exa.method_9();
                if (listing == null) return null;
                string[] lines;
                if (!_lines.TryGetValue(listing, out lines))
                {
                    lines = listing.Split('\n');
                    for (int i = 0; i < lines.Length; i++) lines[i] = lines[i].Trim();
                    if (_lines.Count > 64) _lines.Clear(); // runaway guard; refills next record
                    _lines[listing] = lines;
                }
                int idx = instr.maybe_0.method_2();
                return idx >= 0 && idx < lines.Length && lines[idx].Length > 0 ? lines[idx] : null;
            }
            catch { return null; }
        }

        private sealed class ReferenceComparer : IEqualityComparer<string>
        {
            public static readonly ReferenceComparer Instance = new ReferenceComparer();
            public bool Equals(string a, string b) { return ReferenceEquals(a, b); }
            public int GetHashCode(string s)
            { return System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(s); }
        }
    }
}
