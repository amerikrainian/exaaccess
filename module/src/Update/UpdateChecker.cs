using System;
using System.IO;
using System.Net;
using System.Reflection;
using System.Threading;

namespace Echopunks.Update
{
    /// <summary>
    /// Fetches the newest GitHub release on a thread-pool thread and holds the answer for the
    /// module's tick to announce: the request never touches the game's main thread. Only a release
    /// strictly newer than the running build ever surfaces; up to date, ahead of the release (a dev
    /// build), offline, rate-limited or no release published at all (404) stay spoken-silent with
    /// a log line. Started once per game launch (generation 1 — a dev reload does not ask again).
    /// Ported from Guildrun Access; net48 shape: HttpWebRequest (System.dll, no extra reference),
    /// and TLS 1.2 switched on explicitly — the process is the GAME's exe, whose own target
    /// framework decides the default protocol set, and GitHub refuses anything older.
    /// </summary>
    internal sealed class UpdateChecker
    {
        private const string ApiUrl = "https://api.github.com/repos/amerikrainian/echopunks/releases/latest";

        private volatile string _newerVersion;

        /// <summary>The version to announce, set once the background request found a release
        /// strictly newer than the running build; null before that, and forever when none is.</summary>
        public string NewerVersion => _newerVersion;

        /// <summary>The running mod version: the module assembly's InformationalVersion (the
        /// release Version of Directory.Build.props), build metadata cut.</summary>
        public static string LocalVersion()
        {
            try
            {
                var attr = typeof(UpdateChecker).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>();
                return UpdateCheck.CleanLocal(attr?.InformationalVersion);
            }
            catch { return null; }
        }

        public void Start(string local)
        {
            if (string.IsNullOrEmpty(local))
            {
                Log.Warning("[update] no local version to compare - check skipped");
                return;
            }
            ThreadPool.QueueUserWorkItem(_ => Check(local));
        }

        // The whole request on the thread-pool thread, blocking there. Never throws.
        private void Check(string local)
        {
            try
            {
                ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
                // ECHOPUNKS_UPDATE_URL overrides the feed, like the installer's
                // ECHOPUNKS_INSTALLER_RELEASES_URL: an end-to-end check can point at any
                // latest-release payload without publishing one.
                string url = Environment.GetEnvironmentVariable("ECHOPUNKS_UPDATE_URL");
                if (string.IsNullOrWhiteSpace(url)) url = ApiUrl;
                var request = (HttpWebRequest)WebRequest.Create(url);
                request.UserAgent = "Echopunks-mod";
                request.Accept = "application/vnd.github+json";
                request.Timeout = 10000;
                request.ReadWriteTimeout = 10000;
                string json;
                using (var response = request.GetResponse())
                using (var reader = new StreamReader(response.GetResponseStream()))
                    json = reader.ReadToEnd();

                string remote = UpdateCheck.LatestVersion(json);
                if (remote == null)
                {
                    Log.Warning("[update] release payload named no version");
                    return;
                }
                if (UpdateCheck.IsNewer(remote, local))
                {
                    Log.Info("[update] " + remote + " available (running " + local + ")");
                    _newerVersion = remote;
                }
                else
                {
                    Log.Info("[update] up to date (latest " + remote + ", running " + local + ")");
                }
            }
            catch (Exception e)
            {
                Log.Warning("[update] check failed (" + e.Message + ")");
            }
        }
    }
}
