using System;
using System.Collections.Generic;
using System.IO;
using System.Web.Script.Serialization;

namespace ExaAccess
{
    /// <summary>
    /// Minimal user configuration: a flat {"dotted.key": "value"} JSON file at
    /// %LOCALAPPDATA%\ExaAccess\settings.json — the same on-disk shape as WrathAccess's settings
    /// persistence, so the full Setting-tree port can adopt the file unchanged later. Read once,
    /// lazily; absent or broken file just means defaults. Host-side (the speech stack reads it
    /// before any module exists). Parsing is in-box JavaScriptSerializer — no shipped JSON library.
    /// </summary>
    internal static class HostConfig
    {
        private static Dictionary<string, string> _values;
        private static readonly object Gate = new object();

        internal static string SettingsPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ExaAccess", "settings.json");

        public static string Get(string key, string fallback)
        {
            lock (Gate)
            {
                if (_values == null) _values = LoadFile(SettingsPath);
                string v;
                return _values.TryGetValue(key, out v) && !string.IsNullOrEmpty(v) ? v : fallback;
            }
        }

        /// <summary>Test seam: forget the cache so the next Get re-reads SettingsPath.</summary>
        internal static void ResetForTests()
        {
            lock (Gate) _values = null;
        }

        private static Dictionary<string, string> LoadFile(string path)
        {
            try
            {
                if (!File.Exists(path)) return new Dictionary<string, string>();
                var raw = new JavaScriptSerializer().Deserialize<Dictionary<string, object>>(File.ReadAllText(path))
                          ?? new Dictionary<string, object>();
                var result = new Dictionary<string, string>();
                foreach (var kv in raw)
                    result[kv.Key] = kv.Value?.ToString();
                Log.Info("[config] loaded " + result.Count + " setting(s) from " + path);
                return result;
            }
            catch (Exception ex)
            {
                Log.Warning("[config] could not read " + path + " (" + ex.Message + ") — using defaults.");
                return new Dictionary<string, string>();
            }
        }
    }
}
