using System;

namespace ExaAccess.UI
{
    /// <summary>
    /// Watches the screen stack once per frame and announces transitions — the first real accessibility
    /// feature. A FrameLoop step (registered by Bootstrap), not a patch: the patch layer only provides
    /// the frame heartbeat. Obfuscated (`#=q…`) screen names are logged but never spoken (hard rule);
    /// they get spoken once mapped in <see cref="ScreenNames"/>.
    /// </summary>
    internal sealed class ScreenAnnouncer
    {
        public static readonly ScreenAnnouncer Instance = new ScreenAnnouncer();

        private string _lastScreen;

        /// <summary>Forget the last-seen screen so the next tick announces whatever is active. Called
        /// after game init so the opening screen is announced.</summary>
        public void Reset() => _lastScreen = null;

        public void Tick()
        {
            string name = GameState.TopScreenName();
            if (name == null || name == _lastScreen) return;
            _lastScreen = name;
            Log.Info("[screen] -> " + name);
            if (!ScreenNames.IsObfuscated(name))
                Speech.Tts.Speak(ScreenNames.Friendly(name));
        }
    }
}
