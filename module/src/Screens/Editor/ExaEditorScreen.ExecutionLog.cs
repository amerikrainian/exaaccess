using System;
using ExaAccess.Localization;
using ExaAccess.UI;
using ExaAccess.UI.Graph;

namespace ExaAccess.Screens
{
    public sealed partial class ExaEditorScreen
    {
        // ---- the EXECUTION LOG stop: every instruction the player's EXAs executed in the
        // current/last run, ONE REGION PER CYCLE so Ctrl+Up/Down hop between cycles and arrows
        // read the instructions within one ("XA: COPY 1 X" — the same name-colon-text shape the
        // step echo speaks). The STORE is uncapped (user rule 2026-08-25) — the whole run stays
        // browsable — but the GRAPH materializes only a WINDOW of cycles around the focused one
        // (rebuilt per operation and per frame, so the window recenters on the focus during the
        // very rebuild a move runs against: scrolling to the window's edge always finds the next
        // chunk already loaded; Home/End jump to the whole LOG's edges via OnJumpEdge — the
        // default edge walk would stop at the window's rim). "Go to cycle" is its OWN Tab stop right before the log (user
        // rule 2026-08-25): a synthetic text field — type a number (digits polled from our own
        // keyboard snapshot — no game widget backs this field), Enter jumps to the nearest
        // matching cycle, newest-first on ties. Cycle numbers restart on each test run and
        // battle round, so when the log spans several tests each region names its test. Fed by
        // the ExecutionCapture patch; cleared on each arming, so the last run stays browsable
        // after it stops. Absent while empty, like the test log. Battle mode included — enemy
        // EXAs are filtered at capture (their code draws no window for anyone). ----

        private const int WindowRegions = 51; // materialized regions around a window's anchor
        private const int WindowRows = 1200;  // row budget guarding extreme fan-out runs

        /// <summary>Grow a window of consecutive group indexes outward from the anchor,
        /// alternating sides, until either budget is spent — at the list's ends the free side
        /// keeps going, so the tail still gets a full window. Shared by the execution and test
        /// log stops.</summary>
        private static void ExpandWindow(int count, int anchor, Func<int, int> rowsAt,
            out int lo, out int hi)
        {
            lo = hi = anchor;
            int rows = rowsAt(anchor);
            bool grew = true;
            while (grew && hi - lo + 1 < WindowRegions && rows < WindowRows)
            {
                grew = false;
                if (hi + 1 < count)
                {
                    hi++;
                    rows += rowsAt(hi);
                    grew = true;
                }
                if (lo > 0 && hi - lo + 1 < WindowRegions && rows < WindowRows)
                {
                    lo--;
                    rows += rowsAt(lo);
                    grew = true;
                }
            }
        }

        private ExecutionLog.CycleKey? _execAnchor; // the window's center; null = follow the tail
        // Rebuilds to keep the anchor PINNED after a programmatic jump: the jump's FocusNode is
        // deferred past the next rebuild, and until it applies the focus still sits on the OLD
        // row — anchor-follow would yank the window back and the target would never materialize.
        private int _execJumpHold;
        private string _gotoBuffer = "";

        private static readonly ControlId GotoId = ControlId.Structural("ed.xlog.goto");

        /// <summary>The (test, cycle) a row/region node id encodes, or null for anything else
        /// (the goto field, the dropped note, other screens' nodes).</summary>
        private static ExecutionLog.CycleKey? ParseExecRowId(ControlId id)
        {
            string s = id?.StructuralKey as string;
            if (s == null || !s.StartsWith("ed.xlog.", StringComparison.Ordinal)) return null;
            string[] parts = s.Substring("ed.xlog.".Length).Split('.');
            int t, c, i;
            if (parts.Length != 3 || !int.TryParse(parts[0], out t) || !int.TryParse(parts[1], out c)
                || !int.TryParse(parts[2], out i)) return null;
            return new ExecutionLog.CycleKey(t, c);
        }

        private void BuildExecutionLog(GraphBuilder b, EditorScreen e)
        {
            var log = Patches.ExecutionCapture.Store;
            if (log.IsEmpty) return;

            // "Go to cycle" — its own Tab stop, one synthetic text field.
            b.BeginStop("execgoto");
            b.AddItem(GotoId, new NodeVtable
            {
                ControlType = ControlTypes.TextField,
                Announcements = new[]
                {
                    new NodeAnnouncement(() => Loc.T("editor.execlog.goto"),
                        kind: AnnouncementKinds.Label),
                    new NodeAnnouncement(() => _gotoBuffer, kind: AnnouncementKinds.Value),
                },
                TextEntry = true,
                TextEchoCaps = false,
                TextValue = () => _gotoBuffer,
                OnActivate = CommitGoto,
            });

            // The anchor follows the focus: the cycle whose row is focused recenters the window
            // on this rebuild. Focus elsewhere keeps the last anchor (Tab away and back returns
            // to the same window, remembered node intact); an anchor the backstop shed — or none
            // yet — follows the tail, the live view of a running sim. Read the PERSISTED cursor,
            // not the render-dependent node: when a re-arm clears the log under focus, the node
            // id goes null while the cursor still names the row — the render-dependent read
            // un-pinned the window exactly then, leaving it tail-sliding under a fast run and
            // the region keys dead (user report, 2026-08-25).
            var focused = ParseExecRowId((Navigation.Active as GraphNavigator)?.FocusCursorId);
            if (_execJumpHold > 0) _execJumpHold--;
            else if (focused != null) _execAnchor = focused;

            var cycles = log.Cycles;
            int anchor = _execAnchor != null ? log.IndexOf(_execAnchor.Value) : -1;
            if (anchor < 0) anchor = cycles.Count - 1;
            int lo, hi;
            ExpandWindow(cycles.Count, anchor, i => log.Entries(cycles[i]).Count, out lo, out hi);

            bool multi = log.MultiTest;
            Func<bool, bool> jumpEdge = JumpExecLogEdge; // Home/End = the LOG's edges, not the window's
            Func<int, bool> regionJump = JumpExecTest;   // Ctrl+Up/Down = TESTS on multi-test runs
            b.BeginStop("execlog");
            b.PushContext(Loc.T("editor.execlog"), positions: false);
            for (int ci = lo; ci <= hi; ci++)
            {
                var k = cycles[ci];
                b.SetRegion("cy" + k.Test + "_" + k.Cycle);
                b.PushContext(multi
                    ? Loc.T("editor.execlog.testcycle", new { test = k.Test + 1, n = k.Cycle })
                    : Loc.T("editor.cycle", new { n = k.Cycle }));
                var entries = log.Entries(k);
                for (int i = 0; i < entries.Count; i++)
                {
                    string text = entries[i];
                    b.AddItem(ControlId.Structural("ed.xlog." + k.Test + "." + k.Cycle + "." + i),
                        new NodeVtable
                        {
                            ControlType = ControlTypes.Text,
                            Announcements = new[]
                            {
                                new NodeAnnouncement(() => text, kind: AnnouncementKinds.Label),
                            },
                            OnJumpEdge = jumpEdge,
                            OnRegionJump = regionJump,
                        });
                }
                b.PopContext();
            }
            b.SetRegion(null);
            b.PopContext();
        }

        /// <summary>Ctrl+Up/Down on a log row: when the run spans several tests, hop TESTS —
        /// landing on the target test's first row — because a test is the unit the user thinks
        /// in and a cycle is too fine to hop five hundred at a time (user report, 2026-08-25:
        /// "still on T33 instead of going to T32"). Single-test runs return false and fall
        /// through to the normal per-cycle region hop — there is no coarser unit to hop. The
        /// jump goes through the store directly (anchor + deferred FocusNode, like goto and
        /// Home/End): the adjacent test's rows are usually NOT in the materialized window, so
        /// the graph's own region move could never reach them.</summary>
        private bool JumpExecTest(int dir)
        {
            var log = Patches.ExecutionCapture.Store;
            if (!log.MultiTest) return false;
            var focused = ParseExecRowId((Navigation.Active as GraphNavigator)?.FocusCursorId);
            if (focused == null) return false;
            var cycles = log.Cycles;
            int i = log.IndexOf(focused.Value);
            if (i < 0) return false;
            int test = cycles[i].Test;
            if (dir > 0)
            {
                while (i < cycles.Count && cycles[i].Test == test) i++;
                if (i >= cycles.Count) return true; // newest test already — consume, like a region edge
            }
            else
            {
                while (i >= 0 && cycles[i].Test == test) i--;
                if (i < 0) return true; // oldest test already
                int prev = cycles[i].Test;
                while (i > 0 && cycles[i - 1].Test == prev) i--;
            }
            var key = cycles[i];
            _execAnchor = key;
            _execJumpHold = 2;
            Navigation.FocusNode(ControlId.Structural("ed.xlog." + key.Test + "." + key.Cycle + ".0"));
            return true;
        }

        /// <summary>Home/End on a log row: jump to the whole log's first/last row — the graph
        /// only holds the window, so the default edge walk would stop at the window's rim. Same
        /// O(1) mechanism as the goto jump: recenter the anchor, then the deferred FocusNode
        /// lands on a row the next rebuild has materialized.</summary>
        private bool JumpExecLogEdge(bool first)
        {
            var log = Patches.ExecutionCapture.Store;
            if (log.IsEmpty) return false;
            var cycles = log.Cycles;
            var key = cycles[first ? 0 : cycles.Count - 1];
            int row = first ? 0 : log.Entries(key).Count - 1;
            _execAnchor = key;
            _execJumpHold = 2;
            Navigation.FocusNode(ControlId.Structural("ed.xlog." + key.Test + "." + key.Cycle + "." + row));
            return true;
        }

        /// <summary>Enter on the goto field: jump to the typed cycle — the nearest existing
        /// cycle number when the exact one is absent, newest-first on ties (the end of a run is
        /// the part being debugged). Cycle numbers restart per test, so a bare number favors the
        /// NEWEST test; "test.cycle" (1-based test, matching the spoken "Test n" labels) pins an
        /// earlier one. The landing announces itself through the normal focus differ, so a
        /// nearest-match is instantly audible.</summary>
        private void CommitGoto()
        {
            var log = Patches.ExecutionCapture.Store;
            if (log.IsEmpty) return;
            int n, wantTest = -1;
            int dot = _gotoBuffer.IndexOf('.');
            if (dot >= 0)
            {
                int t;
                if (!int.TryParse(_gotoBuffer.Substring(0, dot), out t)
                    || !int.TryParse(_gotoBuffer.Substring(dot + 1), out n)) return;
                wantTest = t - 1;
            }
            else if (!int.TryParse(_gotoBuffer, out n)) return;
            _gotoBuffer = "";
            var cycles = log.Cycles;
            int best = -1, bestDist = int.MaxValue;
            for (int pass = 0; pass < 2 && best < 0; pass++, wantTest = -1) // unknown test: retry unpinned
                for (int i = cycles.Count - 1; i >= 0; i--)
                {
                    if (wantTest >= 0 && cycles[i].Test != wantTest) continue;
                    int d = Math.Abs(cycles[i].Cycle - n);
                    if (d < bestDist)
                    {
                        bestDist = d;
                        best = i;
                        if (d == 0) break;
                    }
                }
            if (best < 0) return;
            var key = cycles[best];
            _execAnchor = key; // recenter BEFORE the deferred focus applies, so the target exists
            _execJumpHold = 2;
            Navigation.FocusNode(ControlId.Structural("ed.xlog." + key.Test + "." + key.Cycle + ".0"));
        }

        /// <summary>Per-frame: the goto field is SYNTHETIC (no game widget consumes typing for
        /// it), so digits and Backspace are read straight off our keyboard snapshot while it is
        /// focused — the navigator's TextValue watch echoes them like any field.</summary>
        private void PollGotoTyping()
        {
            if (!GotoId.Equals(Navigation.FocusedNodeId))
            {
                // Left the stop with an uncommitted number — stale, drop it.
                if (_gotoBuffer.Length > 0 && !"execlog".Equals(Navigation.FocusedStopKey))
                    _gotoBuffer = "";
                return;
            }
            if (Input.SdlKeyboard.CtrlHeld || Input.SdlKeyboard.AltHeld) return;
            for (int sc = (int)Input.Scancode.Num1; sc <= (int)Input.Scancode.Num0; sc++)
                if (Input.SdlKeyboard.JustPressed(sc) && _gotoBuffer.Length < 10)
                    _gotoBuffer += (char)('0' + (sc == (int)Input.Scancode.Num0 ? 0 : sc - 29));
            if (Input.SdlKeyboard.JustPressed((int)Input.Scancode.Period)
                && _gotoBuffer.Length > 0 && _gotoBuffer.Length < 10 && _gotoBuffer.IndexOf('.') < 0)
                _gotoBuffer += '.'; // "test.cycle" — pin a specific test's cycle
            if (Input.SdlKeyboard.JustPressed((int)Input.Scancode.Backspace) && _gotoBuffer.Length > 0)
                _gotoBuffer = _gotoBuffer.Substring(0, _gotoBuffer.Length - 1);
        }
    }
}
