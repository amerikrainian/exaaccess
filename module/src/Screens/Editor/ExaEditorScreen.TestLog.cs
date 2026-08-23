using ExaAccess.Localization;
using ExaAccess.UI;
using ExaAccess.UI.Graph;

namespace ExaAccess.Screens
{
    public sealed partial class ExaEditorScreen
    {
        // ---- the TEST LOG stop: every sim event of the current/last run (EXA errors, goal
        // flips), ONE REGION PER TEST RUN so Ctrl+Up/Down hop between tests and arrows read
        // within one. Free runs no longer voice routine errors live (user rule 2026-08-23: a
        // fan-out solution's probes dying by design, times 100 auto-advancing tests, was
        // spam — a free run speaks goal FAILURES only; stepping still speaks everything);
        // this stop is where every event lands regardless. Cleared on each arming. Absent
        // while empty, like Problems. ----

        private readonly TestLog _testLog = new TestLog();

        private void BuildTestLog(GraphBuilder b, EditorScreen e)
        {
            if (_testLog.IsEmpty) return;
            b.BeginStop("testlog");
            b.PushContext(Loc.T("editor.testlog"), positions: false);
            foreach (int test in _testLog.Tests)
            {
                int t = test;
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
                    });
                }
                int more = _testLog.Overflow(t);
                if (more > 0)
                    b.AddItem(ControlId.Structural("ed.tlog." + t + ".more"), new NodeVtable
                    {
                        ControlType = ControlTypes.Text,
                        Announcements = new[]
                        {
                            new NodeAnnouncement(() => Loc.T("editor.file.more", new { n = more }),
                                kind: AnnouncementKinds.Label),
                        },
                    });
                b.PopContext();
            }
            b.SetRegion(null);
            b.PopContext();
        }
    }
}
