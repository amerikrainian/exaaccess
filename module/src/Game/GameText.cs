using System;
using System.Collections.Generic;
using System.Reflection;

namespace ExaAccess.Game
{
    /// <summary>
    /// The GAME's own localized strings (deob GClass7.smethod_5(key, comment) → LocString, resolved
    /// against the live language — EXAPUNKS ships English/German/French/Russian/Chinese/Japanese).
    /// HARD RULE COROLLARY: anything the game has a label for is read from HERE, never duplicated in
    /// our locale tables — ui.json is only for text the game genuinely doesn't have (role words,
    /// hints, prompts, names for unlabeled things). The game's loc keys are literally the English
    /// text, so an unresolved lookup falls back to the key itself and stays readable.
    /// </summary>
    internal static class GameText
    {
        private static MethodInfo _lookup; // static LocString smethod_5(string key, string comment)
        private static bool _resolved;
        private static readonly Dictionary<string, object> Cache = new Dictionary<string, object>(); // key → LocString

        /// <summary>The game's current text for a loc key ("Fullscreen", "Exit Game", …).</summary>
        public static string T(string key)
        {
            var ls = LocStringFor(key);
            if (ls != null)
            {
                try
                {
                    string s = ls.ToString(); // LocString.ToString resolves via the live language
                    if (!string.IsNullOrEmpty(s)) return s;
                }
                catch { }
            }
            return key;
        }

        /// <summary>T, massaged for speech: the game separates alternatives with " / " (read
        /// unreliably by TTS) and embeds newlines/backslashes in some labels.</summary>
        public static string TSpeech(string key)
            => T(key).Replace(" / ", ", ").Replace("\n", " ").Replace("\\", "");

        private static object LocStringFor(string key)
        {
            if (key == null) return null;
            object cached;
            if (Cache.TryGetValue(key, out cached)) return cached;
            Resolve();
            object ls = null;
            if (_lookup != null)
            {
                try { ls = _lookup.Invoke(null, new object[] { key, string.Empty }); }
                catch (Exception ex) { Log.Error("[gametext] lookup failed for '" + key + "'", ex); }
            }
            Cache[key] = ls; // negative results cache too — no per-frame retry storms
            return ls;
        }

        private static void Resolve()
        {
            if (_resolved) return;
            _resolved = true;
            var asm = GameState.GameAssembly;
            var locString = asm?.GetType("LocString");
            var language = asm?.GetType("Language");
            if (locString == null || language == null)
            {
                Log.Error("[gametext] LocString/Language types not found — falling back to key text.");
                return;
            }

            // The loc registry (deob GClass7): the static class holding both a static Language field
            // and a unique static (string, string) → LocString method.
            foreach (var t in asm.ManifestModule.GetTypes())
            {
                if (!(t.IsAbstract && t.IsSealed)) continue;
                bool hasLanguageField = false;
                foreach (var f in t.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static))
                    if (f.FieldType == language) { hasLanguageField = true; break; }
                if (!hasLanguageField) continue;

                MethodInfo unique = null;
                foreach (var m in t.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.DeclaredOnly))
                {
                    if (m.ReturnType != locString) continue;
                    var ps = m.GetParameters();
                    if (ps.Length != 2 || ps[0].ParameterType != typeof(string) || ps[1].ParameterType != typeof(string)) continue;
                    if (unique != null) { unique = null; break; }
                    unique = m;
                }
                if (unique != null)
                {
                    _lookup = unique;
                    Log.Info("[gametext] game localization resolved: " + t.Name + "." + unique.Name);
                    return;
                }
            }
            Log.Error("[gametext] game localization registry not found by shape — falling back to key text.");
        }
    }
}
