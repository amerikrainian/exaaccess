using System;

namespace ExaAccess.Modularity
{
    /// <summary>
    /// The contract between the permanent HOST (this assembly — bootstrap, logging, GameState, the
    /// init/tick Harmony patches, the speech backend, the dev server) and the reloadable MODULE
    /// (ExaAccess.Module.dll — every feature: announcers, localization, input, UI). The split exists
    /// for hot reload (pattern ported from NonVisualCalculus): the module is byte-loaded so its dll is
    /// never file-locked, and a rebuild + /reload swaps features into the running game without
    /// a restart or another trip through the boot click-gate.
    ///
    /// Rules a module implementation must follow (each learned the hard way in NVC):
    ///  • Any Harmony patching uses a PER-LOAD UNIQUE id (e.g. "com.exaaccess.module." + Guid) and
    ///    UnpatchAll(ownId) in Dispose. The host loads the NEW module before disposing the OLD one (so a
    ///    failed reload keeps the old module running) — with a fixed id, the old module's teardown
    ///    would strip the fresh load's patches.
    ///  • Never cache game objects across frames beyond what a reload can cheaply rebuild; re-derive
    ///    from GameState. Nothing is migrated across a swap — the new module starts cold.
    ///  • Own no native handles and register nothing irrevocable with the game; anything like that
    ///    belongs in the host (e.g. the Prism speech backend lives host-side for exactly this reason).
    /// </summary>
    public interface IModModule : IDisposable
    {
        /// <summary>Wire up: register frame steps, apply module-owned patches, load data files.
        /// Runs on the main thread. Throwing aborts the load; the previous module (if any) stays.</summary>
        void Load(ModHost host);

        /// <summary>Once per frame, on the game's main thread, only after game init (the tick hook
        /// doesn't run during the boot splash).</summary>
        void Tick();
    }

    /// <summary>What the host tells the module about the world. The module also links against the
    /// host assembly directly (Log, GameState, Tts, MemberResolver are shared statics — one copy,
    /// owned by the host); this class carries only the state that varies per load.</summary>
    public sealed class ModHost
    {
        /// <summary>True once GameLogic's init has completed (the first screen exists).</summary>
        public bool GameInitialized { get; internal set; }

        /// <summary>1 for the boot load, +1 per successful reload. Lets a reloaded module skip
        /// boot-only behavior (e.g. the "ready" announcement).</summary>
        public int ModuleGeneration { get; internal set; }
    }
}
