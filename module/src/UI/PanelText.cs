using System.Collections.Generic;

namespace ExaAccess.UI
{
    /// <summary>One string the game drew, with its position. BCL-pure (unit-tested) — the capture
    /// patch translates the game's Vector2 to bare floats before anything reaches this file.</summary>
    internal struct PanelCell
    {
        public string Text;
        public float X;
        public float Y;

        public PanelCell(string text, float x, float y)
        {
            Text = text;
            X = x;
            Y = y;
        }
    }

    /// <summary>
    /// Reconstructs reading order from positioned text: the special-puzzle panels (I/O logs, status
    /// readouts) are drawn as loose strings, but their layout is a grid — table rows share a y,
    /// columns an x. Cells cluster into rows by y (game coordinates are y-up, so reading order is
    /// descending y), cells within a row read left to right joined with ", ", and a multi-line
    /// string becomes one row per line. Exact repeats collapse (a panel drawn twice between two of
    /// our ticks must not double its rows).
    /// </summary>
    internal static class PanelText
    {
        /// <summary>Table rows step 41.5 apart; same-row cells share y exactly or nearly.</summary>
        private const float RowTolerance = 10f;

        public static List<string> Assemble(IEnumerable<PanelCell> cells)
        {
            var kept = new List<PanelCell>();
            var seen = new HashSet<string>();
            foreach (var cell in cells)
            {
                if (string.IsNullOrWhiteSpace(cell.Text)) continue;
                if (!seen.Add(cell.Text + "\n" + cell.X + "\n" + cell.Y)) continue;
                kept.Add(cell);
            }
            kept.Sort((a, b) => a.Y != b.Y ? b.Y.CompareTo(a.Y) : a.X.CompareTo(b.X));

            var lines = new List<string>();
            for (int i = 0; i < kept.Count;)
            {
                float rowY = kept[i].Y;
                int end = i + 1;
                while (end < kept.Count && rowY - kept[end].Y <= RowTolerance) end++;
                var row = kept.GetRange(i, end - i);
                row.Sort((a, b) => a.X.CompareTo(b.X));
                var texts = new List<string>();
                foreach (var cell in row) texts.Add(cell.Text.Trim());
                foreach (var line in string.Join(", ", texts).Split('\n'))
                {
                    string trimmed = line.Trim();
                    if (trimmed.Length > 0) lines.Add(trimmed);
                }
                i = end;
            }
            return lines;
        }
    }
}
