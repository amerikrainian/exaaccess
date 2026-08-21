using System;
using System.Text;
using System.Text.RegularExpressions;

namespace ExaAccess.Speech
{
    /// <summary>
    /// The call-site speech facade — the mod's single output chokepoint (hard rule). Cleans
    /// game-sourced whitespace, mirrors every line to the DEBUG dev tap, and routes through
    /// <see cref="SpeechManager"/>'s handler chain (Prism → SAPI → clipboard, or the user's
    /// configured output). Never interrupts by default (the SayTheSpire house preference).
    /// Host-side: the engines it fronts hold native/OS resources that survive module reloads.
    /// Thread-safe — the engine gate lives in SpeechManager.
    /// </summary>
    public static class Tts
    {
        private static readonly Regex Whitespace = new Regex(@"\s+", RegexOptions.Compiled);

        public static bool Ready => SpeechManager.HasLoadedHandler;

#if DEBUG
        /// <summary>Dev-only tap: every spoken string is mirrored here so the dev server's /speech log
        /// can read back what was said (we can't hear the TTS). Null in a normal run.</summary>
        public static Action<string> Observer;
        private static void Tap(string text) { if (!string.IsNullOrEmpty(text)) { try { Observer?.Invoke(text); } catch { } } }
#else
        private static void Tap(string text) { }
#endif

        /// <summary>Warm up the configured output now so boot can announce (and log) the outcome.
        /// Safe to call once at boot; false means no engine loaded — Speak stays a silent no-op.</summary>
        public static bool Init() => SpeechManager.WarmUp();

        /// <summary>Speak (and braille, where the engine supports it). Queued by default.</summary>
        public static void Speak(string text, bool interrupt = false)
        {
            if (string.IsNullOrEmpty(text)) return;
            text = Clean(text);
            if (string.IsNullOrEmpty(text)) return;
            Tap(text);
            SpeechManager.Output(text, interrupt);
        }

        public static void Stop() => SpeechManager.Silence();

        public static void Shutdown() => SpeechManager.Shutdown();

        // EXAPUNKS labels aren't TMP rich text (that was WotR/Unity), but many strings carry embedded
        // newlines/tabs and runs of spaces — collapse them so speech doesn't stutter.
        // Internal for the unit tests.
        internal static string Clean(string s)
        {
            if (string.IsNullOrEmpty(s)) return s;
            var sb = new StringBuilder(s.Length);
            foreach (char c in s)
                sb.Append(char.IsControl(c) ? ' ' : c);
            return Whitespace.Replace(sb.ToString(), " ").Trim();
        }
    }
}
