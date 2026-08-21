using System;
using System.Collections.Generic;
using System.IO;
using System.Web.Script.Serialization;

namespace ExaAccess.Localization
{
    /// <summary>
    /// Locale tables, ported from WrathAccess. Layout: &lt;root&gt;/&lt;lang&gt;/&lt;table&gt;.json, each file a flat
    /// {"dotted.key": "value"} dictionary; table name = file name. enGB is the complete fallback
    /// manifest, loaded first; another language is just a dropped-in folder. Lookup: current language
    /// â†’ enGB â†’ warn + null (Message then falls back to the key text â€” never silent, never throws).
    ///
    /// Module-side ON PURPOSE: statics are per-load, so a hot reload re-reads the JSON â€” edit a
    /// string, /reload, hear it. The root defaults next to the HOST assembly (this module is
    /// byte-loaded and has no on-disk location): &lt;gameDir&gt;/ExaAccess/locale.
    ///
    /// Language selection is a pluggable source (<see cref="LanguageSource"/>), polled per frame like
    /// WrathAccess polls the game â€” EXAPUNKS's own language setting can plug in once mapped. Default
    /// is enGB. Parsing is in-box JavaScriptSerializer â€” no shipped JSON library.
    /// </summary>
    public static class LocalizationManager
    {
        public const string Fallback = "enGB";

        /// <summary>Returns the active language folder name (e.g. "enGB"). Null source = fallback.</summary>
        public static Func<string> LanguageSource;

        private static readonly Dictionary<string, Dictionary<string, string>> Tables =
            new Dictionary<string, Dictionary<string, string>>();
        private static readonly Dictionary<string, Dictionary<string, string>> FallbackTables =
            new Dictionary<string, Dictionary<string, string>>();
        private static string _language = Fallback;
        private static string _root;

        public static string Language => _language;

        public static string DefaultRoot =>
            Path.Combine(Path.GetDirectoryName(typeof(Log).Assembly.Location), "ExaAccess", "locale");

        /// <summary>Load enGB (the fallback manifest) and the current language, and install the
        /// Message resolver. First thing the module does on load.</summary>
        public static void Initialize(string root = null)
        {
            _root = root ?? DefaultRoot;
            FallbackTables.Clear();
            Tables.Clear();
            LoadLanguage(Fallback, FallbackTables);
            _language = CurrentLanguage();
            if (_language != Fallback) LoadLanguage(_language, Tables);
            Message.LocalizationResolver = Get;
            Log.Info("[loc] initialized: " + FallbackTables.Count + " fallback table(s), language " + _language + ", root " + _root);
        }

        /// <summary>Per-frame language poll (a FrameLoop step): on change, swap the current tables.</summary>
        public static void Tick()
        {
            string lang = CurrentLanguage();
            if (lang == _language) return;
            _language = lang;
            Tables.Clear();
            if (lang != Fallback) LoadLanguage(lang, Tables);
            Log.Info("[loc] language changed to " + lang);
        }

        public static string Get(string table, string key)
        {
            if (_language != Fallback
                && Tables.TryGetValue(table, out var t) && t.TryGetValue(key, out var v))
                return v;
            if (FallbackTables.TryGetValue(table, out var ft) && ft.TryGetValue(key, out var fv))
                return fv;
            Log.Warning("[loc] missing string: " + table + "." + key);
            return null;
        }

        /// <summary>Like Get but quiet â€” for keys that legitimately may not exist (e.g. a screen
        /// label that falls back to a de-camelled type name).</summary>
        public static string GetOrDefault(string table, string key, string fallback)
        {
            if (_language != Fallback
                && Tables.TryGetValue(table, out var t) && t.TryGetValue(key, out var v))
                return v;
            if (FallbackTables.TryGetValue(table, out var ft) && ft.TryGetValue(key, out var fv))
                return fv;
            return fallback;
        }

        private static string CurrentLanguage()
        {
            try { return LanguageSource?.Invoke() ?? Fallback; }
            catch { return Fallback; }
        }

        private static void LoadLanguage(string lang, Dictionary<string, Dictionary<string, string>> into)
        {
            string dir = Path.Combine(_root, lang);
            if (!Directory.Exists(dir))
            {
                if (lang == Fallback) Log.Warning("[loc] no locale dir: " + dir + " â€” speech falls back to key names.");
                else Log.Info("[loc] no locale dir for " + lang + " â€” using " + Fallback + ".");
                return;
            }
            foreach (string file in Directory.GetFiles(dir, "*.json"))
            {
                try
                {
                    var entries = new JavaScriptSerializer().Deserialize<Dictionary<string, string>>(File.ReadAllText(file));
                    if (entries != null)
                        into[Path.GetFileNameWithoutExtension(file)] = entries;
                }
                catch (Exception ex)
                {
                    Log.Error("[loc] failed to parse " + file + ": " + ex.Message);
                }
            }
        }
    }
}

