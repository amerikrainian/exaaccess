using System;
using System.Text;
using System.Text.RegularExpressions;

namespace ExaAccess.Speech
{
    /// <summary>
    /// The call-site speech facade. For the POC this is a deliberately thin wrapper over Prism's
    /// best-available backend — no settings tree, no SAPI/clipboard handlers, no positional render.
    /// Prism's <c>registry_create_best</c> already picks the running screen reader (NVDA/JAWS) or falls
    /// back to SAPI/OneCore, so a single backend covers everyone for now. The richer config-driven
    /// multi-handler stack from WrathAccess can be ported later behind this same <c>Speak</c>/<c>Stop</c>
    /// surface without touching call sites.
    ///
    /// Never interrupts by default (queued speech is the SayTheSpire house preference). Thread-safe:
    /// the game hooks (main thread) and the dev server (its own thread) both call in, so every native
    /// round-trip is serialized under one lock.
    /// </summary>
    public static class Tts
    {
        private static readonly object Gate = new object();
        private static IntPtr _ctx = IntPtr.Zero;
        private static IntPtr _backend = IntPtr.Zero;
        private static PrismNative.BackendFeatures _features;
        private static bool _ready;

        private static readonly Regex Whitespace = new Regex(@"\s+", RegexOptions.Compiled);

        public static bool Ready => _ready;

#if DEBUG
        /// <summary>Dev-only tap: every spoken string is mirrored here so the dev server's /speech log
        /// can read back what was said (we can't hear the TTS). Null in a normal run.</summary>
        public static Action<string> Observer;
        private static void Tap(string text) { if (!string.IsNullOrEmpty(text)) { try { Observer?.Invoke(text); } catch { } } }
#else
        private static void Tap(string text) { }
#endif

        /// <summary>Bring up Prism on the best available backend. Safe to call once at boot; returns
        /// false (and stays silent, never throwing) if prism.dll is missing or no backend loads.</summary>
        public static bool Init()
        {
            lock (Gate)
            {
                if (_ready) return true;
                try
                {
                    _ctx = PrismNative.Init(IntPtr.Zero);
                    if (_ctx == IntPtr.Zero) { Log.Error("[speech] prism_init returned null (dll loaded but init failed)."); return false; }

                    _backend = PrismNative.RegistryCreateBest(_ctx);
                    if (_backend == IntPtr.Zero) { Log.Error("[speech] no usable Prism backend on this machine."); Shutdown(); return false; }

                    var err = PrismNative.BackendInitialize(_backend);
                    if (err != PrismNative.PrismError.Ok && err != PrismNative.PrismError.AlreadyInitialized)
                    {
                        Log.Error("[speech] backend initialize failed (" + err + ").");
                        Shutdown();
                        return false;
                    }

                    _features = (PrismNative.BackendFeatures)PrismNative.BackendGetFeatures(_backend);
                    _ready = true;
                    Log.Info("[speech] Prism backend: " + (PrismNative.BackendName(_backend) ?? "<unknown>")
                        + " (features=0x" + ((ulong)_features).ToString("X") + ")");
                    return true;
                }
                catch (DllNotFoundException)
                {
                    Log.Error("[speech] prism.dll not found next to EXAPUNKS.exe (or a dependency is missing, e.g. the VC++ runtime).");
                    return false;
                }
                catch (Exception ex)
                {
                    Log.Error("[speech] Prism init failed", ex);
                    Shutdown();
                    return false;
                }
            }
        }

        /// <summary>Speak (and braille, where the backend supports it) <paramref name="text"/>. Cleans
        /// game-sourced whitespace first. No-op before <see cref="Init"/> succeeds.</summary>
        public static void Speak(string text, bool interrupt = false)
        {
            if (string.IsNullOrEmpty(text)) return;
            text = Clean(text);
            if (string.IsNullOrEmpty(text)) return;
            Tap(text);
            if (!_ready) return;
            lock (Gate)
            {
                if (_backend == IntPtr.Zero) return;
                try
                {
                    // prism_backend_output drives speech + braille when supported; else plain speak.
                    if ((_features & PrismNative.BackendFeatures.SupportsOutput) != 0)
                    {
                        var err = PrismNative.BackendOutput(_backend, text, interrupt);
                        if (err == PrismNative.PrismError.Ok) return;
                        if (err != PrismNative.PrismError.NotImplemented)
                            Log.Info("[speech] output -> " + err + ", falling back to speak.");
                    }
                    PrismNative.BackendSpeak(_backend, text, interrupt);
                }
                catch (Exception ex) { Log.Error("[speech] Speak failed: " + ex.Message); }
            }
        }

        public static void Stop()
        {
            if (!_ready) return;
            lock (Gate)
            {
                if (_backend == IntPtr.Zero) return;
                try { PrismNative.BackendStop(_backend); } catch { }
            }
        }

        public static void Shutdown()
        {
            lock (Gate)
            {
                if (_backend != IntPtr.Zero)
                {
                    try { PrismNative.BackendStop(_backend); } catch { }
                    try { PrismNative.BackendFree(_backend); } catch { }
                    _backend = IntPtr.Zero;
                }
                if (_ctx != IntPtr.Zero)
                {
                    try { PrismNative.Shutdown(_ctx); } catch { }
                    _ctx = IntPtr.Zero;
                }
                _features = 0;
                _ready = false;
            }
        }

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
