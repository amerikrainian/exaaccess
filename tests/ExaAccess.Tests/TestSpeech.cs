using System;
using System.Collections.Generic;
using ExaAccess.Speech;

namespace ExaAccess.Tests
{
    /// <summary>Routes the whole speech stack into a capturing fake for the duration of a test —
    /// everything spoken through Tts (navigator, screens, fallbacks) lands in <see cref="Spoken"/>.</summary>
    internal sealed class TestSpeech : IDisposable
    {
        private sealed class CaptureHandler : ISpeechHandler
        {
            public readonly List<string> Lines = new List<string>();
            public string Key => "capture";
            public bool Detect() => true;
            public bool Load() => true;
            public void Unload() { }
            public bool Speak(string text, bool interrupt) { Lines.Add(text); return true; }
            public bool Output(string text, bool interrupt) => Speak(text, interrupt);
            public void Silence() { }
        }

        private readonly CaptureHandler _handler = new CaptureHandler();

        public IReadOnlyList<string> Spoken => _handler.Lines;

        public TestSpeech()
        {
            Environment.SetEnvironmentVariable("EXAACCESS_SPEECH", "auto");
            SpeechManager.ResetForTests(new List<ISpeechHandler> { _handler });
        }

        public void Dispose()
        {
            Environment.SetEnvironmentVariable("EXAACCESS_SPEECH", null);
            SpeechManager.ResetForTests(new List<ISpeechHandler>
            {
                new PrismHandler(), new SapiHandler(), new ClipboardHandler(),
            });
        }
    }
}
