#if DEBUG
using System;
using System.Collections.Concurrent;
using System.Text;
using System.Threading;

namespace ExaAccess.Dev
{
    /// <summary>
    /// Dev-only in-process driver, gated behind the EXAACCESS_DEV env var (or a marker file). Exposes a
    /// loopback HTTP server so an external driver (Claude, curl) can introspect and drive the live mod/game
    /// while it runs — the test harness copied over from the WrathAccess architecture, trimmed to what the
    /// EXAPUNKS POC needs:
    ///
    ///   GET  /health         liveness.
    ///   POST /say            speak the request body through the real speech path (also lands in /speech).
    ///   GET  /speech?since=N lines the mod has spoken since cursor N (we can't hear the TTS, so this is
    ///                        how we observe it). Tapped at the Tts chokepoint.
    ///   GET  /screen         the active screen + the whole screen stack (bottom → top), by type name.
    ///   POST /eval           body = C# source, run against the live game on the main thread (REPL state
    ///                        persists across calls); returns captured output + result/errors.
    ///
    /// /eval and /screen run on the game's main thread: HTTP requests enqueue a job and block until
    /// <see cref="Pump"/> (called once per frame from the GameLogic.method_25 prefix) executes it. /say and
    /// /speech are thread-safe and answer directly off the HTTP thread.
    ///
    /// This whole subsystem is compiled only in DEBUG (#if DEBUG) — a Release build has none of it. Even in
    /// Debug it stays inert unless EXAACCESS_DEV=1 (or the marker file exists).
    /// </summary>
    internal sealed class DevServer
    {
        public static readonly DevServer Instance = new DevServer();

        public const string EnableEnv = "EXAACCESS_DEV";
        public const string PortEnv = "EXAACCESS_DEV_PORT";
        public const string MarkerFile = "devserver.enable"; // in the working dir (the game folder)
        private const int DefaultPort = 8772; // WotR uses 8771; keep ours distinct.

        private static bool DevEnabled(out string how)
        {
            how = null;
            if (Environment.GetEnvironmentVariable(EnableEnv) == "1") { how = "env"; return true; }
            try
            {
                if (System.IO.File.Exists(System.IO.Path.Combine(Environment.CurrentDirectory, MarkerFile)))
                { how = "marker"; return true; }
            }
            catch { }
            return false;
        }

        private sealed class Job
        {
            public Func<string> Work;
            public string Result = "";
            public readonly ManualResetEventSlim Done = new ManualResetEventSlim(false);
        }

        private readonly SpeechLog _speech = new SpeechLog();
        private readonly CSharpEvaluator _evaluator = new CSharpEvaluator();
        private readonly ConcurrentQueue<Job> _jobs = new ConcurrentQueue<Job>();
        private DevHttpServer _http;
        private bool _enabled;

        public void Start()
        {
            string how;
            if (!DevEnabled(out how)) return;

            int port = DefaultPort;
            string p = Environment.GetEnvironmentVariable(PortEnv);
            if (!string.IsNullOrEmpty(p)) int.TryParse(p, out port);

            // Tap every string the mod speaks into the ring buffer so /speech can read it back.
            ExaAccess.Speech.Tts.Observer = _speech.Add;

            try
            {
                _http = new DevHttpServer(port, HandleRequest);
                _http.Start();
                _enabled = true;
                Log.Info("Dev server on http://127.0.0.1:" + port + " (gate: " + how + "; GET /health /speech /screen, POST /say /eval)");
            }
            catch (Exception e)
            {
                Log.Error("Dev server failed to start", e);
            }
        }

        /// <summary>Run queued main-thread jobs. Called once per frame from the GameLogic tick prefix.</summary>
        public void Pump()
        {
            if (!_enabled) return;
            Job job;
            while (_jobs.TryDequeue(out job))
            {
                try { job.Result = job.Work() ?? ""; }
                catch (Exception e) { job.Result = "[host error] " + e + "\n"; }
                job.Done.Set();
            }
        }

        private string OnMainThread(Func<string> work, int timeoutSeconds = 30)
        {
            var job = new Job { Work = work };
            _jobs.Enqueue(job);
            if (!job.Done.Wait(TimeSpan.FromSeconds(timeoutSeconds)))
                return "[timeout] main thread did not run the job within " + timeoutSeconds + "s (frozen / not ticking?)\n";
            return job.Result;
        }

        // Runs on the HTTP thread.
        private string HandleRequest(string method, string path, string body)
        {
            string route = path;
            string query = "";
            int q = path.IndexOf('?');
            if (q >= 0) { route = path.Substring(0, q); query = path.Substring(q + 1); }

            if (route == "/health" || route == "/") return "ok\n";

            if (route == "/say" && method == "POST")
            {
                string text = (body ?? "").Trim();
                if (text.Length == 0) return "[empty] POST the text to speak as the body\n";
                ExaAccess.Speech.Tts.Speak(text, interrupt: true);
                return "said: " + text + "\n";
            }

            if (route == "/speech" && method == "GET")
            {
                long since = 0;
                foreach (string kv in query.Split('&'))
                    if (kv.StartsWith("since=", StringComparison.Ordinal))
                        long.TryParse(kv.Substring("since=".Length), out since);
                long next;
                string lines = _speech.Render(since, out next);
                return "cursor: " + next + "\n" + lines;
            }

            if (route == "/screen" && method == "GET")
                return OnMainThread(DumpScreens);

            if (route == "/eval" && method == "POST")
            {
                if (string.IsNullOrWhiteSpace(body)) return "[empty] POST C# source as the request body\n";
                return OnMainThread(() => _evaluator.Eval(body));
            }

            return "[404] " + method + " " + route + "\n";
        }

        private static string DumpScreens()
        {
            if (!GameState.Bound) return "[not bound] GameState hasn't resolved GameLogic yet\n";
            var names = GameState.ScreenStackNames();
            if (names.Count == 0) return "(no screens on the stack yet)\n";
            var sb = new StringBuilder();
            sb.Append("active: ").Append(names[names.Count - 1]).Append('\n');
            sb.Append("stack (bottom -> top):\n");
            for (int i = 0; i < names.Count; i++)
                sb.Append("  ").Append(i).Append(": ").Append(names[i]).Append('\n');
            return sb.ToString();
        }
    }
}
#endif
