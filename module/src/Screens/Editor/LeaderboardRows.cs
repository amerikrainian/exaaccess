using System;
using System.Collections.Generic;
using Echopunks.Game;
using Echopunks.Localization;
using Echopunks.UI;
using Echopunks.UI.Graph;

namespace Echopunks.Screens
{
    /// <summary>The histogram/leaderboard PANEL as browsable rows — ONE Tab stop per stat
    /// (user rule, 2026-08-22: Tab jumps Cycles/Size/Activity like the three drawn panels;
    /// arrows stay within a stat). The game draws the same panel (Theme.smethod_5) in two
    /// places from the same per-stat GClass296 (scoreManager method_14) — the
    /// puzzle-completion screen's Leaderboards view and the solution browser's right half —
    /// and both build through here, each mirroring ITS drawn variant: the completion layout
    /// speaks the "Currently N[, previously M]" caption and marks the histogram from the
    /// run's score (maybe_3, GEnum216 0); the browser layout has NO caption (the selected
    /// row already spoke the scores) and marks from the SELECTED SOLUTION's score (maybe_2,
    /// GEnum216 1) — suppressed when the solution is over the size limit, exactly like the
    /// drawn arrow. Per stat: the caption (when the layout has one), the percentile cutoffs
    /// + friends' scores merged best-first (each behind its own Steam option, like the
    /// drawing), then one row per NON-EMPTY histogram bin — "lo to hi: N%", percent of the
    /// fullest bin (the server sends peak-normalized shape, no counts), the marker's bucket
    /// tagged ", your score" (it speaks even at 0%). Empty bins are skipped: the gap reads
    /// from the ranges.</summary>
    internal static class LeaderboardRows
    {
        /// <summary>One stat panel as one Tab stop ("lb.{si}"). <paramref name="marker"/> is
        /// the score whose bucket reads ", your score" — pass the same value the screen's own
        /// Theme call selects (empty = no marker, the drawn arrow's absence).</summary>
        public static void BuildStat(GraphBuilder b, GClass296 g, int si, bool caption, Maybe<int> marker)
        {
            b.BeginStop("lb." + si);
            b.PushContext(StatName(g), positions: false);
            if (caption)
            {
                string cap = CurrentCaption(g);
                if (cap != null) AddRow(b, "lb." + si + ".cur", cap);
            }
            var entries = EntryRows(g);
            for (int i = 0; i < entries.Count; i++)
                AddRow(b, "lb." + si + ".e" + i, entries[i]);
            AddBinRows(b, g, si, marker);
            b.PopContext();
        }

        private static string StatName(GClass296 g)
        {
            try
            {
                switch (g.genum178_0)
                {
                    case GEnum178.Cycles: return ScoreManager.locString_0.ToString();
                    case GEnum178.Size: return ScoreManager.locString_1.ToString();
                    default: return ScoreManager.locString_2.ToString();
                }
            }
            catch { return null; }
        }

        // Mirrors the game's caption under each histogram (hardcoded literals game-side).
        private static string CurrentCaption(GClass296 g)
        {
            try
            {
                if (!g.maybe_3.method_0()) return null;
                int cur = g.maybe_3.method_2();
                if (!g.maybe_2.method_0()) return Loc.T("lb.current", new { value = cur });
                int prev = g.maybe_2.method_2();
                return prev == cur
                    ? Loc.T("lb.current.same", new { value = cur })
                    : Loc.T("lb.current.prev", new { value = cur, prev });
            }
            catch { return null; }
        }

        // The list the game draws under each histogram: TOP/TENTH percentile cutoffs plus the
        // friends leaderboard, merged and sorted ascending (best first — lower is better for
        // all three stats), each behind its own game option.
        private static List<string> EntryRows(GClass296 g)
        {
            var list = new List<Tuple<string, int>>();
            try
            {
                var gl = GameLogic.gameLogic_0;
                if (gl.gclass17_0.gclass52_13.method_0())
                {
                    var v = gl.scoreManager_0.method_13(g.string_0, Percentile.Top);
                    if (v.method_0()) list.Add(Tuple.Create(GameText.T("TOP_PERCENTILE"), v.method_2()));
                }
                if (gl.gclass17_0.gclass52_14.method_0())
                {
                    var v = gl.scoreManager_0.method_13(g.string_0, Percentile.Tenth);
                    if (v.method_0()) list.Add(Tuple.Create(GameText.T("TENTH_PERCENTILE"), v.method_2()));
                }
                if (gl.gclass17_0.gclass52_11.method_0())
                {
                    var friends = gl.scoreManager_0.method_12(g.string_0);
                    if (friends.method_0())
                        foreach (var e in friends.method_2().list_0)
                            list.Add(Tuple.Create(e.string_0, e.int_0));
                }
            }
            catch { }
            list.Sort((a, b) => a.Item2.CompareTo(b.Item2));
            var rows = new List<string>();
            foreach (var t in list)
                rows.Add(Loc.T("lb.entry", new { name = t.Item1, value = t.Item2 }));
            return rows;
        }

        private static void AddBinRows(GraphBuilder b, GClass296 g, int si, Maybe<int> marker)
        {
            try
            {
                if (!GameLogic.gameLogic_0.gclass17_0.gclass52_10.method_0()) return;
                if (!g.maybe_0.method_0()) return;
                var hist = g.maybe_0.method_2();
                if (hist == null || hist.int_0 <= 0 || hist.float_0 == null || hist.float_0.Length == 0) return;
                int len = hist.float_0.Length, max = hist.int_0;
                int yours = -1;
                if (marker.method_0()) // the game's marker-bucket formula
                    yours = Math.Max(0, Math.Min(len - 1, (marker.method_2() - 1) * len / max));
                for (int j = 0; j < len; j++)
                {
                    int pct = (int)Math.Round(hist.float_0[j] * 100f);
                    if (pct <= 0 && j != yours) continue;
                    // The bucket→score range must be the exact INVERSE of the game's
                    // score→bucket map ((s-1)*len/max): ceil-based bounds. The floor form
                    // drifted a value low whenever max doesn't divide by len — an activity
                    // score of 7 (max 20, 16 bins) read as "6" instead of "6 to 7".
                    int lo = (j * max + len - 1) / len + 1, hi = ((j + 1) * max - 1) / len + 1;
                    if (lo > hi) continue; // a bucket no integer score can land in
                    string text = lo == hi // a single-value bin reads as just the number
                        ? Loc.T(j == yours ? "lb.bin.one.yours" : "lb.bin.one", new { lo, pct })
                        : Loc.T(j == yours ? "lb.bin.yours" : "lb.bin", new { lo, hi, pct });
                    AddRow(b, "lb." + si + ".b" + j, text);
                }
            }
            catch { }
        }

        public static void AddRow(GraphBuilder b, string id, string text)
        {
            b.AddItem(ControlId.Structural(id), new NodeVtable
            {
                ControlType = ControlTypes.Text,
                SpeaksOwnPosition = true, // no "n of m" in these panels (user rule)
                Announcements = new[]
                {
                    new NodeAnnouncement(() => text, kind: AnnouncementKinds.Label),
                },
            });
        }
    }
}
