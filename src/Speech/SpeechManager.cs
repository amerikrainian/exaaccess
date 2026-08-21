using System;
using System.Collections.Generic;

namespace ExaAccess.Speech
{
    /// <summary>
    /// Handler registry + selection, ported from WrathAccess: a fixed priority list (Prism first —
    /// the user's own screen reader — then SAPI, then clipboard), lazy Detect/Load per handler, and
    /// an auto chain that walks the list until something loads. The user picks an output with the
    /// `speech.output` key in %LOCALAPPDATA%\ExaAccess\settings.json ("auto" | "prism" | "sapi" |
    /// "clipboard"; the EXAACCESS_SPEECH env var overrides for dev runs); anything unknown or broken
    /// resolves back through auto — never strand a blind user with no voice.
    /// Host-side and stateful across module reloads. All engine calls serialize under one gate: the
    /// game thread and the dev server's HTTP thread both speak.
    /// </summary>
    internal static class SpeechManager
    {
        private static readonly object Gate = new object();
        private static readonly HashSet<ISpeechHandler> Loaded = new HashSet<ISpeechHandler>();

        // Priority order IS the auto chain. Internal-settable so tests can inject fakes.
        internal static IList<ISpeechHandler> Handlers = new List<ISpeechHandler>
        {
            new PrismHandler(),
            new SapiHandler(),
            new ClipboardHandler(),
        };

        public static string OutputKey
        {
            get
            {
                string env = Environment.GetEnvironmentVariable("EXAACCESS_SPEECH");
                if (!string.IsNullOrEmpty(env)) return env;
                return HostConfig.Get("speech.output", "auto");
            }
        }

        public static bool HasLoadedHandler { get { lock (Gate) return Loaded.Count > 0; } }

        /// <summary>Resolve OutputKey to a loaded handler now (boot warm-up), so the first real
        /// utterance doesn't pay the load cost and boot can log/announce the outcome.</summary>
        public static bool WarmUp()
        {
            lock (Gate) return ResolveHandler(OutputKey) != null;
        }

        public static void Output(string text, bool interrupt)
        {
            lock (Gate)
            {
                var handler = ResolveHandler(OutputKey);
                if (handler == null) return;
                try
                {
                    if (handler.Output(text, interrupt)) return;
                    // The chosen engine rejected the utterance (device lost, backend died): retry down
                    // the priority chain PAST the failing handler rather than going silent.
                    foreach (var h in Handlers)
                    {
                        if (ReferenceEquals(h, handler)) continue;
                        if (EnsureLoaded(h) && h.Output(text, interrupt)) return;
                    }
                    Log.Warning("[speech] every handler rejected an utterance.");
                }
                catch (Exception ex) { Log.Error("[speech] output failed: " + ex.Message); }
            }
        }

        public static void Silence()
        {
            lock (Gate)
            {
                foreach (var h in Loaded)
                    try { h.Silence(); } catch { }
            }
        }

        public static void Shutdown()
        {
            lock (Gate)
            {
                foreach (var h in Loaded)
                    try { h.Unload(); } catch { }
                Loaded.Clear();
            }
        }

        /// <summary>"auto"/null/"" walks the priority list to the first loadable handler; a known key
        /// loads that handler or falls back to auto; an unknown key logs and goes auto. Call under
        /// <see cref="Gate"/>.</summary>
        internal static ISpeechHandler ResolveHandler(string key)
        {
            if (string.IsNullOrEmpty(key) || key == "auto")
            {
                foreach (var handler in Handlers)
                    if (EnsureLoaded(handler)) return handler;
                Log.Error("[speech] no speech handler could be loaded!");
                return null;
            }

            foreach (var handler in Handlers)
                if (handler.Key == key)
                    return EnsureLoaded(handler) ? handler : ResolveHandler("auto");

            Log.Error("[speech] unknown speech output '" + key + "' — using auto.");
            return ResolveHandler("auto");
        }

        private static bool EnsureLoaded(ISpeechHandler handler)
        {
            if (Loaded.Contains(handler)) return true;
            try
            {
                if (!handler.Detect()) { Log.Info("[speech] " + handler.Key + ": not detected on this machine."); return false; }
                if (!handler.Load()) { Log.Info("[speech] " + handler.Key + ": detected but failed to load."); return false; }
                Loaded.Add(handler);
                Log.Info("[speech] handler loaded: " + handler.Key);
                return true;
            }
            catch (Exception ex) { Log.Error("[speech] handler " + handler.Key + " failed: " + ex.Message); }
            return false;
        }

        /// <summary>Test seam: forget every loaded handler without unloading (fakes own no resources).</summary>
        internal static void ResetForTests(IList<ISpeechHandler> handlers)
        {
            lock (Gate)
            {
                Loaded.Clear();
                Handlers = handlers;
            }
        }
    }
}
