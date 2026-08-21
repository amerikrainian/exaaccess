using System;
using ExaAccess.Localization;
using ExaAccess.Modularity;

namespace ExaAccess
{
    /// <summary>
    /// The module entry point — what the host's ModuleLoader instantiates. Composition root for all
    /// reloadable features: localization first (everything speakable depends on it), then FrameLoop
    /// steps and (as they land) input, the UI graph, and module-owned Harmony patches. All statics in
    /// this assembly are per-load — a reload starts this whole half of the mod cold, re-deriving
    /// everything from the live game (and re-reading the locale files).
    /// </summary>
    public sealed class ExaAccessModule : IModModule
    {
        private ModHost _host;
        private bool _announcedReady;

        public void Load(ModHost host)
        {
            _host = host;
            LocalizationManager.Initialize();

            // Per-frame steps, in the order they run. (The dev pump is host-side, before Module.Tick.)
            FrameLoop.Register("loc", LocalizationManager.Tick);
            FrameLoop.Register("screens", UI.ScreenAnnouncer.Instance.Tick);

            // Boot load only — a hot reload mid-session shouldn't re-greet.
            if (host.ModuleGeneration == 1)
                Speech.Tts.Speak(Loc.T("app.starting"));
        }

        public void Tick()
        {
            if (!_announcedReady && _host.GameInitialized)
            {
                _announcedReady = true;
                if (_host.ModuleGeneration == 1)
                    Speech.Tts.Speak(Loc.T("app.ready"));
            }
            FrameLoop.Tick();
        }

        public void Dispose()
        {
            // Nothing to tear down yet: no module-owned Harmony patches, no event subscriptions on
            // host or game state. When patches land here, this is where UnpatchSelf goes.
        }
    }
}
