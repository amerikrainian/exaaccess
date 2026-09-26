using System;
using Echopunks.Localization;
using Echopunks.UI;
using Echopunks.UI.Graph;

namespace Echopunks.Screens
{
    public sealed partial class ExaEditorScreen
    {
        // ---- the TEST LOG stop: every sim event of the current/last run (EXA errors, goal
        // flips), ONE REGION PER TEST RUN so Ctrl+Up/Down hop between tests and arrows read
        // within one. Free runs no longer voice routine errors live (user rule 2026-08-23: a
        // fan-out solution's probes dying by design, times 100 auto-advancing tests, was
        // spam — a free run speaks goal FAILURES only; stepping still speaks everything);
        // this stop is where every event lands regardless. The store is UNCAPPED (user rule
        // 2026-08-25 — the old newest-24-tests ring stopped a 100-test sweep's browse at its
        // window edge); the graph materializes a WINDOW of tests around the focused one, same
        // mechanics as the execution log. Cleared on each arming. Absent while empty, like
        // Problems. ----

        private readonly TestLog _testLog = new TestLog();
        private int? _testAnchor; // the window's center; null = follow the newest test
        private int _testJumpHold; // pins the anchor while a jump's deferred focus applies (see exec log)

        /// <summary>The test a test-log row id encodes, or null for anything else.</summary>
        private static int? ParseTestRowId(ControlId id)
        {
            string s = id?.StructuralKey as string;
            if (s == null || !s.StartsWith("ed.tlog.", StringComparison.Ordinal)) return null;
            string[] parts = s.Substring("ed.tlog.".Length).Split('.');
            int t, i;
            if (parts.Length != 2 || !int.TryParse(parts[0], out t) || !int.TryParse(parts[1], out i))
                return null;
            return t;
        }

        private void BuildTestLog(GraphBuilder b, EditorScreen e)
        {
            if (_testLog.IsEmpty) return;

            // Same anchor-follows-focus windowing as the execution log (see there — including
            // reading the persisted cursor, which survives the focused row vanishing).
            var focused = ParseTestRowId((Navigation.Active as GraphNavigator)?.FocusCursorId);
            if (_testJumpHold > 0) _testJumpHold--;
            else if (focused != null) _testAnchor = focused;
            var tests = _testLog.Tests;
            int anchor = _testAnchor != null ? _testLog.IndexOf(_testAnchor.Value) : -1;
            if (anchor < 0) anchor = tests.Count - 1;
            int lo, hi;
            ExpandWindow(tests.Count, anchor, i => _testLog.Entries(tests[i]).Count, out lo, out hi);

            Func<bool, bool> jumpEdge = JumpTestLogEdge; // Home/End = the LOG's edges, not the window's
            b.BeginStop("testlog");
            b.PushContext(Loc.T("editor.testlog"), positions: false);
            for (int ti = lo; ti <= hi; ti++)
            {
                int t = tests[ti];
                b.SetRegion("test" + t);
                b.PushContext(Loc.T("editor.testlog.test", new { n = t + 1 }));
                var entries = _testLog.Entries(t);
                for (int i = 0; i < entries.Count; i++)
                {
                    string text = entries[i];
                    b.AddItem(ControlId.Structural("ed.tlog." + t + "." + i), new NodeVtable
                    {
                        ControlType = ControlTypes.Text,
                        Announcements = new[]
                        {
                            new NodeAnnouncement(() => text, kind: AnnouncementKinds.Label),
                        },
                        OnJumpEdge = jumpEdge,
                    });
                }
                b.PopContext();
            }
            b.SetRegion(null);
            b.PopContext();
        }

        /// <summary>Home/End on a test-log row: the whole log's first/last row (see the
        /// execution log's twin — the graph only holds the window).</summary>
        private bool JumpTestLogEdge(bool first)
        {
            if (_testLog.IsEmpty) return false;
            var tests = _testLog.Tests;
            int t = tests[first ? 0 : tests.Count - 1];
            int row = first ? 0 : _testLog.Entries(t).Count - 1;
            _testAnchor = t;
            _testJumpHold = 2;
            Navigation.FocusNode(ControlId.Structural("ed.tlog." + t + "." + row));
            return true;
        }
    }
}
