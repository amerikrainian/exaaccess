using System;
using ExaAccess.Localization;
using ExaAccess.Modularity;
using HarmonyLib;

namespace ExaAccess
{
    /// <summary>
    /// The module entry point — what the host's ModuleLoader instantiates. Composition root for all
    /// reloadable features: localization first (everything speakable depends on it), then the module's
    /// own Harmony patches (per-load unique id — see IModModule), announcer hooks, and FrameLoop steps.
    /// All statics in this assembly are per-load — a reload starts this whole half of the mod cold,
    /// re-deriving everything from the live game (and re-reading the locale files).
    /// </summary>
    public sealed class ExaAccessModule : IModModule
    {
        private ModHost _host;
        private Harmony _harmony;

        public void Load(ModHost host)
        {
            _host = host;
            LocalizationManager.Initialize();

            // The announcer's localized wording hooks (the graph core itself never touches Loc).
            UI.Graph.GraphAnnouncer.PositionText = (index, count) => Loc.T("nav.position", new { index, count });
            UI.Graph.GraphAnnouncer.ExpandedStateText = e => Loc.T(e ? "role.expanded" : "role.collapsed");

            // Module-owned patches: per-load UNIQUE id so THIS load's Dispose unpatches exactly these
            // (the host loads the new module before disposing the old — a fixed id would let the old
            // teardown strip the fresh patches; see IModModule).
            _harmony = new Harmony("com.exaaccess.module." + Guid.NewGuid().ToString("N"));
            if (!host.GameInitialized)
                Patches.SplashPatches.Apply(_harmony); // pre-init only: the splash is long gone on a reload

            // Per-frame steps, in the order they run. (The dev pump is host-side, before Module.Tick.)
            FrameLoop.Register("keyboard", Input.SdlKeyboard.Update);
            FrameLoop.Register("input", Input.InputManager.Tick);
            FrameLoop.Register("loc", LocalizationManager.Tick);
            FrameLoop.Register("screens", UI.ScreenAnnouncer.Instance.Tick);

            // One greeting, right at boot (the game window isn't even up yet), so the user knows the
            // mod is alive before the splash prompt arrives. A hot reload mid-session doesn't re-greet.
            if (host.ModuleGeneration == 1)
                Speech.Tts.Speak(Loc.T("app.ready"));
        }

        public void Tick() => FrameLoop.Tick();

        public void Dispose()
        {
            // This Harmony build predates UnpatchSelf; UnpatchAll(ownId) is the same owner-scoped removal.
            try { _harmony?.UnpatchAll(_harmony.Id); }
            catch (Exception ex) { Log.Error("[module] UnpatchSelf failed", ex); }
            _harmony = null;
        }
    }
}
