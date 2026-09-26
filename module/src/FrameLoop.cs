using System;
using System.Collections.Generic;

namespace Echopunks
{
    /// <summary>
    /// The mod's per-frame dispatcher — the one consumer of the GameLogic tick prefix. Subsystems that
    /// need frame time (dev pump, screen watcher, input polling, UI) register a named step at boot;
    /// the tick hook calls <see cref="Tick"/>. Mirrors WrathAccess's Ticker in role: one ordered,
    /// defensive loop instead of every subsystem growing its own patch. Steps run in registration order
    /// (Bootstrap is the composition root and owns that order); a throwing step is logged and skipped
    /// for that frame, never unregistered and never allowed to break the frame — an accessibility mod
    /// must not crash the game.
    /// </summary>
    internal static class FrameLoop
    {
        private struct Step
        {
            public string Name;
            public Action Run;
        }

        // Registration happens once at boot (main thread, before the game loop starts); ticks happen on
        // the game's main thread only. No lock needed under that contract.
        private static readonly List<Step> Steps = new List<Step>();

        public static void Register(string name, Action run)
        {
            if (run == null) return;
            Steps.Add(new Step { Name = name, Run = run });
            Log.Info("[frameloop] step registered: " + name);
        }

        /// <summary>Run every registered step, in order. Called from the GameLogic tick prefix.</summary>
        public static void Tick()
        {
            for (int i = 0; i < Steps.Count; i++)
            {
                try { Steps[i].Run(); }
                catch (Exception ex) { Log.Error("[frameloop] step '" + Steps[i].Name + "' failed", ex); }
            }
        }
    }
}
