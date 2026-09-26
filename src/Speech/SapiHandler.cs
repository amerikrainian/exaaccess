using System;
using System.Speech.Synthesis;

namespace Echopunks.Speech
{
    /// <summary>
    /// SAPI fallback for machines with no screen reader and no working Prism. WrathAccess drives SAPI
    /// through 272 lines of hand-rolled IDispatch COM because Unity's Mono can't host the interop —
    /// this is real .NET Framework 4.8, so the in-box System.Speech synthesizer replaces all of it.
    /// Async speech (never blocks the game thread); interrupt cancels the queue first.
    /// </summary>
    internal sealed class SapiHandler : ISpeechHandler
    {
        private SpeechSynthesizer _synth;

        public string Key => "sapi";

        public bool Detect() => true; // in-box on every Windows this game runs on

        public bool Load()
        {
            try
            {
                _synth = new SpeechSynthesizer();
                _synth.SetOutputToDefaultAudioDevice();
                Log.Info("[speech] SAPI voice: " + (_synth.Voice?.Name ?? "<default>"));
                return true;
            }
            catch (Exception ex)
            {
                Log.Error("[speech] SAPI load failed", ex);
                Unload();
                return false;
            }
        }

        public void Unload()
        {
            try { _synth?.Dispose(); } catch { }
            _synth = null;
        }

        public bool Speak(string text, bool interrupt)
        {
            if (_synth == null) return false;
            try
            {
                if (interrupt) _synth.SpeakAsyncCancelAll();
                _synth.SpeakAsync(text);
                return true;
            }
            catch (Exception ex)
            {
                Log.Error("[speech] SAPI speak failed: " + ex.Message);
                return false;
            }
        }

        public bool Output(string text, bool interrupt) => Speak(text, interrupt); // no braille via SAPI

        public void Silence()
        {
            try { _synth?.SpeakAsyncCancelAll(); } catch { }
        }
    }
}
