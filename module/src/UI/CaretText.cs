using ExaAccess.Localization;

namespace ExaAccess.UI
{
    /// <summary>
    /// Game-free caret/text helpers behind the code editor's narration (extracted so they unit-test
    /// without the game assembly — the editor screen's own statics need Burbank to load).
    /// </summary>
    public static class CaretText
    {
        /// <summary>0-based line index of a character offset in a '\n'-joined text.</summary>
        public static int LineIndex(string text, int caret)
        {
            int line = 0;
            for (int i = 0; i < caret && i < text.Length; i++)
                if (text[i] == '\n') line++;
            return line;
        }

        /// <summary>The character to the right of the caret — the one the cursor "sits on".</summary>
        public static string CharAt(string text, int caret)
        {
            if (caret >= text.Length || text[caret] == '\n') return Loc.T("text.endofline");
            char c = text[caret];
            return c == ' ' ? Loc.T("text.space") : c.ToString();
        }

        /// <summary>The whole word the caret landed on (a word jump's destination) — the contiguous
        /// non-space run from the caret; delimiter landings fall back to the character forms.</summary>
        public static string WordAt(string text, int caret)
        {
            if (caret >= text.Length || text[caret] == '\n') return Loc.T("text.endofline");
            if (text[caret] == ' ') return Loc.T("text.space");
            int end = caret;
            while (end < text.Length && text[end] != ' ' && text[end] != '\n') end++;
            return text.Substring(caret, end - caret);
        }
    }
}
