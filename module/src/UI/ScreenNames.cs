using System;
using System.Text;

namespace ExaAccess.UI
{
    /// <summary>
    /// Maps a screen's obfuscation-recovered TYPE name to something speakable, and knows which names are
    /// still Eazfuscator gibberish (never speak those — CLAUDE.md hard rule). Extracted from the patch
    /// layer: this is presentation, shared by the screen-change announcer and (soon) the UI graph, and
    /// unit-testable without the game.
    /// </summary>
    internal static class ScreenNames
    {
        /// <summary>Speakable label for a screen type name. Curated screens live in the locale table
        /// ("screen.&lt;TypeName&gt;" in ui.json — the localizable manifest); anything unmapped is
        /// de-CamelCased so it's at least intelligible. Callers filter obfuscated names first.</summary>
        public static string Friendly(string typeName)
        {
            return Localization.LocalizationManager.GetOrDefault("ui", "screen." + typeName, FallbackLabel(typeName));
        }

        /// <summary>The label for a screen with no locale entry: strip the Screen suffix, de-camel.</summary>
        internal static string FallbackLabel(string typeName)
        {
            string s = typeName;
            if (s.EndsWith("Screen", StringComparison.Ordinal)) s = s.Substring(0, s.Length - "Screen".Length);
            return DeCamel(s);
        }

        /// <summary>True when a type name is (or contains) Eazfuscator gibberish that de4dot could not
        /// rename semantically (`#=q…`, or any non-identifier characters).</summary>
        public static bool IsObfuscated(string name)
        {
            if (string.IsNullOrEmpty(name)) return true;
            if (name.StartsWith("#=q", StringComparison.Ordinal)) return true;
            foreach (char c in name)
                if (!char.IsLetterOrDigit(c) && c != '_') return true;
            return false;
        }

        internal static string DeCamel(string s)
        {
            if (string.IsNullOrEmpty(s)) return s;
            var sb = new StringBuilder(s.Length + 8);
            for (int i = 0; i < s.Length; i++)
            {
                char c = s[i];
                if (i > 0 && char.IsUpper(c) && !char.IsUpper(s[i - 1])) sb.Append(' ');
                sb.Append(c);
            }
            return sb.ToString();
        }
    }
}
