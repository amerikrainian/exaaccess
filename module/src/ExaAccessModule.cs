using System;
using ExaAccess.Modularity;

namespace ExaAccess
{
    /// <summary>
    /// The module entry point — what the host's ModuleLoader instantiates. Composition root for all
    /// reloadable features: wires FrameLoop steps and (as they land) localization, input, the UI
    /// graph, and module-owned Harmony patches. All statics in this assembly are per-load — a reload
    /// starts this whole half of the mod cold, re-deriving everything from the live game.
    /// </summary>
    public sealed class ExaAccessModule : IModModule
    {
        private ModHost _host;
        private bool _announcedReady;

        public void Load(ModHost host)
        {
            _host = host;
            // Per-frame steps, in the order they run. (The dev pump is host-side, before Module.Tick.)
            FrameLoop.Register("screens", UI.ScreenAnnouncer.Instance.Tick);
        }

        public void Tick()
        {
            if (!_announcedReady && _host.GameInitialized)
            {
                _announcedReady = true;
                // Boot load only — a hot reload mid-session shouldn't re-greet.
                if (_host.ModuleGeneration == 1)
                    Speech.Tts.Speak("ExaAccess ready.");
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
