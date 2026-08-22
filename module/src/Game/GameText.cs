using System;

namespace ExaAccess.Game
{
    /// <summary>
    /// The GAME's own localized strings, typed (GClass7.smethod_5(key, comment) → LocString,
    /// resolved against the live language — EXAPUNKS ships six). HARD RULE COROLLARY: anything the
    /// game has a label for is read from HERE, never duplicated in our locale tables. The game's loc
    /// keys are literally the English text, so a failed lookup falls back to the key and stays
    /// readable.
    /// </summary>
    internal static class GameText
    {
        /// <summary>The game's current text for a loc key ("Fullscreen", "Exit Game", …).</summary>
        public static string T(string key)
        {
            if (key == null) return null;
            try
            {
                string s = GClass7.smethod_5(key, string.Empty)?.ToString();
                if (!string.IsNullOrEmpty(s)) return s;
            }
            catch (Exception ex) { Log.Error("[gametext] lookup failed for '" + key + "': " + ex.Message); }
            return key;
        }

        /// <summary>T, massaged for speech: the game separates alternatives with " / " (read
        /// unreliably by TTS) and embeds newlines/backslashes in some labels.</summary>
        public static string TSpeech(string key) => Speech(T(key));

        /// <summary>The same speech massage for a game string obtained some other way (a LocString
        /// read off a game object rather than looked up by key). Also strips the game's inline
        /// emphasis markup (*bold* / _italic_) — but keeps backslash-ESCAPED literals ("HACK\*MATCH"
        /// keeps its asterisk): the escaped forms are shelved on placeholders before the strip.</summary>
        public static string Speech(string s)
        {
            if (s == null) return null;
            s = s.Replace("\\*", "\u0001").Replace("\\_", "\u0002");
            s = s.Replace("*", "").Replace("_", "");
            s = s.Replace("\u0001", "*").Replace("\u0002", "_");
            return s.Replace(" / ", ", ").Replace("\n", " ").Replace("\\", "");
        }
    }
}
