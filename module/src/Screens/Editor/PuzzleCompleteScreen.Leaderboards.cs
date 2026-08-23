using System;
using System.Collections.Generic;
using System.Reflection;
using ExaAccess.Game;
using ExaAccess.Localization;
using ExaAccess.UI;
using ExaAccess.UI.Graph;

namespace ExaAccess.Screens
{
    public sealed partial class PuzzleCompleteScreen
    {
        // ---- the Leaderboards view as a browsable Tab stop. Rows mirror what the game DRAWS,
        // per stat (Cycles / Size / Activity): the "Currently N" caption, the percentile
        // cutoffs and friends' scores merged best-first (the game's own list under each
        // histogram), then the histogram one row per NON-EMPTY bin — "lo to hi: N%", percent
        // of the fullest bin (the bars are peak-normalized; absolute counts never reach the
        // client), with your bin marked (the game's arrow — it speaks even at 0%). Empty bins
        // are skipped: the gap reads from the ranges. Every piece honors the same Steam
        // options the game gates its drawing on. The stop exists only while the Leaderboards
        // view is the SHOWN one (the toggle flips it back) and only while scores are
        // eligible — an over-size solution speaks the game's replacement notice instead. ----

        private static readonly FieldInfo ScoreDictField =
            Deobf.Field(typeof(PuzzleCompletionScreen), "dictionary_0");
        private static FieldInfo _sizeLimitField; // GClass361.int_2, resolved on first use

        private void BuildLeaderboards(GraphBuilder b, PuzzleCompletionScreen s)
        {
            try
            {
                // Rows mirror the SHOWN view: none while Test Run Data is up.
                if (TabField == null || (bool)TabField.GetValue(s)) return;
                var dict = ScoreDictField?.GetValue(s) as Dictionary<GEnum178, GClass296>;
                if (dict == null || dict.Count == 0) return;
                b.BeginStop("leaderboards");
                int size, limit;
                if (!Eligible(s, out size, out limit))
                {
                    AddLbRow(b, "lb.ineligible", Loc.T("lb.ineligible", new { size, limit }));
                    return;
                }
                int si = 0;
                foreach (var kv in dict) BuildStatRows(b, kv.Value, si++);
            }
            catch (Exception ex) { Log.Error("[complete] leaderboards build failed", ex); }
        }

        private void BuildStatRows(GraphBuilder b, GClass296 g, int si)
        {
            b.PushContext(StatName(g), positions: false);
            string caption = CurrentCaption(g);
            if (caption != null) AddLbRow(b, "lb." + si + ".cur", caption);
            var entries = EntryRows(g);
            for (int i = 0; i < entries.Count; i++)
                AddLbRow(b, "lb." + si + ".e" + i, entries[i]);
            AddBinRows(b, g, si);
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

        private void AddBinRows(GraphBuilder b, GClass296 g, int si)
        {
            try
            {
                if (!GameLogic.gameLogic_0.gclass17_0.gclass52_10.method_0()) return;
                if (!g.maybe_0.method_0()) return;
                var hist = g.maybe_0.method_2();
                if (hist == null || hist.int_0 <= 0 || hist.float_0 == null || hist.float_0.Length == 0) return;
                int len = hist.float_0.Length, max = hist.int_0;
                int yours = -1;
                if (g.maybe_3.method_0()) // the game's marker-bucket formula
                    yours = Math.Max(0, Math.Min(len - 1, (g.maybe_3.method_2() - 1) * len / max));
                for (int j = 0; j < len; j++)
                {
                    int pct = (int)Math.Round(hist.float_0[j] * 100f);
                    if (pct <= 0 && j != yours) continue;
                    int lo = max * j / len + 1, hi = max * (j + 1) / len;
                    string text = lo == hi // a single-value bin reads as just the number
                        ? Loc.T(j == yours ? "lb.bin.one.yours" : "lb.bin.one", new { lo, pct })
                        : Loc.T(j == yours ? "lb.bin.yours" : "lb.bin", new { lo, hi, pct });
                    AddLbRow(b, "lb." + si + ".b" + j, text);
                }
            }
            catch { }
        }

        private static void AddLbRow(GraphBuilder b, string id, string text)
        {
            b.AddItem(ControlId.Structural(id), new NodeVtable
            {
                ControlType = ControlTypes.Text,
                SpeaksOwnPosition = true, // no "n of m" anywhere on this screen (user rule)
                Announcements = new[]
                {
                    new NodeAnnouncement(() => text, kind: AnnouncementKinds.Label),
                },
            });
        }

        private static bool Eligible(PuzzleCompletionScreen s, out int size, out int limit)
        {
            size = 0;
            limit = int.MaxValue;
            try
            {
                size = SizeField != null ? (int)SizeField.GetValue(s) : 0;
                var solution = SolutionField?.GetValue(s) as Solution;
                if (solution != null && PuzzleDetailsMethod != null)
                {
                    var details = PuzzleDetailsMethod.Invoke(null, new object[] { solution.method_0() });
                    if (details != null && _sizeLimitField == null)
                        _sizeLimitField = Deobf.Field(details.GetType(), "int_2");
                    if (details != null && _sizeLimitField != null)
                        limit = Convert.ToInt32(_sizeLimitField.GetValue(details));
                }
            }
            catch { }
            return size <= limit;
        }
    }
}
