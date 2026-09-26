namespace Echopunks.Speech
{
    /// <summary>
    /// One speech engine, ported (trimmed) from WrathAccess: Prism (screen readers), SAPI, clipboard.
    /// Handlers are host-side — they own native/OS resources that must survive module hot-reloads.
    /// Detect() is a cheap availability probe, Load() acquires resources (called once, lazily, by
    /// <see cref="SpeechManager.ResolveHandler"/>), and a false/throwing handler just moves the auto
    /// chain to the next one — never strand a blind user with no voice.
    /// WrathAccess's settings-schema / audio-render surface is deliberately not ported yet: there is
    /// no settings UI here to drive it. All calls arrive serialized under SpeechManager's gate.
    /// </summary>
    internal interface ISpeechHandler
    {
        /// <summary>Stable id: "prism", "sapi", "clipboard" — what speech.output selects.</summary>
        string Key { get; }

        bool Detect();
        bool Load();
        void Unload();

        /// <summary>Speak text. Returns false when the engine rejected it (chain falls through).</summary>
        bool Speak(string text, bool interrupt);

        /// <summary>Speech + braille where the engine supports it; else same as Speak.</summary>
        bool Output(string text, bool interrupt);

        void Silence();
    }
}
