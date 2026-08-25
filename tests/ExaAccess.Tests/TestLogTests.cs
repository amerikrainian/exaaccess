using ExaAccess.UI;
using Xunit;

namespace ExaAccess.Tests
{
    /// <summary>The editor's per-run test log: events grouped by test in arrival order,
    /// uncapped (the graph windows what it shows; the store keeps the run) with a runaway
    /// backstop that sheds the oldest tests whole and counts them.</summary>
    public class TestLogTests
    {
        [Fact]
        public void GroupsByTestInArrivalOrder()
        {
            var log = new TestLog();
            Assert.True(log.IsEmpty);
            log.Add(2, "c1");
            log.Add(0, "a1");
            log.Add(2, "c2");
            log.Add(0, "a2");
            Assert.False(log.IsEmpty);
            Assert.Equal(4, log.EntryCount);
            Assert.Equal(new[] { 2, 0 }, log.Tests);
            Assert.Equal(new[] { "c1", "c2" }, log.Entries(2));
            Assert.Equal(new[] { "a1", "a2" }, log.Entries(0));
            Assert.Empty(log.Entries(5));
            Assert.Equal(0, log.DroppedTests);
        }

        [Fact]
        public void IgnoresEmptyTextAndClampsUnknownTest()
        {
            var log = new TestLog();
            log.Add(0, null);
            log.Add(0, "");
            Assert.True(log.IsEmpty);
            log.Add(-1, "x");
            Assert.Equal(new[] { 0 }, log.Tests);
        }

        [Fact]
        public void KeepsEverythingWithinTheBackstop()
        {
            var log = new TestLog();
            for (int t = 0; t < 100; t++)
                for (int i = 0; i < 12; i++) log.Add(t, "t" + t + "e" + i);
            Assert.Equal(100, log.Tests.Count);
            Assert.Equal(1200, log.EntryCount);
            Assert.Equal(0, log.DroppedTests);
            Assert.Equal(12, log.Entries(0).Count);
            Assert.Equal(12, log.Entries(99).Count);
        }

        [Fact]
        public void IndexOfTracksPositions()
        {
            var log = new TestLog();
            log.Add(3, "a");
            log.Add(7, "b");
            Assert.Equal(0, log.IndexOf(3));
            Assert.Equal(1, log.IndexOf(7));
            Assert.Equal(-1, log.IndexOf(5));
            log.Clear();
            Assert.Equal(-1, log.IndexOf(3));
        }

        [Fact]
        public void BackstopShedsOldestTestsWholeAndCounts()
        {
            var log = new TestLog(maxEntries: 50);
            for (int t = 0; t < 30; t++)
                for (int i = 0; i < 2; i++) log.Add(t, "x");
            Assert.True(log.EntryCount <= 50);
            Assert.True(log.DroppedTests > 0);
            Assert.Equal(-1, log.IndexOf(0));
            Assert.Empty(log.Entries(0));
            // Survivors are the contiguous newest tests with a consistent index.
            Assert.Equal(30 - log.Tests.Count, log.Tests[0]);
            for (int i = 0; i < log.Tests.Count; i++)
                Assert.Equal(i, log.IndexOf(log.Tests[i]));
            Assert.Equal(29, log.Tests[log.Tests.Count - 1]);
        }

        [Fact]
        public void ClearEmpties()
        {
            var log = new TestLog();
            log.Add(1, "x");
            log.Clear();
            Assert.True(log.IsEmpty);
            Assert.Equal(0, log.EntryCount);
            Assert.Empty(log.Tests);
            Assert.Empty(log.Entries(1));
        }
    }
}
