using ExaAccess.UI;
using Xunit;

namespace ExaAccess.Tests
{
    /// <summary>The editor's per-run execution log: instructions grouped by (test, cycle) in
    /// arrival order, uncapped (the graph windows what it shows; the store keeps the run) with
    /// a runaway backstop that sheds the oldest cycles whole and counts them; cycle numbers
    /// restart per test, so equal cycle numbers on different tests are distinct.</summary>
    public class ExecutionLogTests
    {
        private static ExecutionLog.CycleKey Key(int test, int cycle)
        {
            return new ExecutionLog.CycleKey(test, cycle);
        }

        [Fact]
        public void GroupsByCycleInArrivalOrder()
        {
            var log = new ExecutionLog();
            Assert.True(log.IsEmpty);
            log.Add(0, 0, "XA: LINK 800");
            log.Add(0, 0, "XB: COPY 1 X");
            log.Add(0, 1, "XA: COPY M X");
            Assert.False(log.IsEmpty);
            Assert.Equal(3, log.EntryCount);
            Assert.Equal(new[] { Key(0, 0), Key(0, 1) }, log.Cycles);
            Assert.Equal(new[] { "XA: LINK 800", "XB: COPY 1 X" }, log.Entries(Key(0, 0)));
            Assert.Equal(new[] { "XA: COPY M X" }, log.Entries(Key(0, 1)));
            Assert.Empty(log.Entries(Key(0, 5)));
            Assert.Equal(0, log.DroppedCycles);
        }

        [Fact]
        public void SameCycleOnDifferentTestsIsDistinct()
        {
            var log = new ExecutionLog();
            log.Add(0, 3, "a");
            log.Add(1, 3, "b");
            Assert.Equal(new[] { Key(0, 3), Key(1, 3) }, log.Cycles);
            Assert.Equal(new[] { "a" }, log.Entries(Key(0, 3)));
            Assert.Equal(new[] { "b" }, log.Entries(Key(1, 3)));
        }

        [Fact]
        public void MultiTestFlagsASecondTestAndClearResets()
        {
            var log = new ExecutionLog();
            Assert.False(log.MultiTest);
            log.Add(0, 0, "a");
            Assert.False(log.MultiTest);
            log.Add(1, 0, "b");
            Assert.True(log.MultiTest);
            log.Clear();
            Assert.False(log.MultiTest);
        }

        [Fact]
        public void IgnoresEmptyTextAndClampsUnknownTest()
        {
            var log = new ExecutionLog();
            log.Add(0, 0, null);
            log.Add(0, 0, "");
            Assert.True(log.IsEmpty);
            log.Add(-1, 2, "x");
            Assert.Equal(new[] { Key(0, 2) }, log.Cycles);
        }

        [Fact]
        public void KeepsEverythingWithinTheBackstop()
        {
            var log = new ExecutionLog();
            for (int c = 0; c < 500; c++)
                for (int i = 0; i < 4; i++) log.Add(0, c, "c" + c + "e" + i);
            Assert.Equal(500, log.Cycles.Count);
            Assert.Equal(2000, log.EntryCount);
            Assert.Equal(0, log.DroppedCycles);
            Assert.Equal(4, log.Entries(Key(0, 0)).Count);
            Assert.Equal(4, log.Entries(Key(0, 499)).Count);
        }

        [Fact]
        public void IndexOfTracksPositions()
        {
            var log = new ExecutionLog();
            log.Add(0, 7, "a");
            log.Add(0, 9, "b");
            log.Add(1, 0, "c");
            Assert.Equal(0, log.IndexOf(Key(0, 7)));
            Assert.Equal(1, log.IndexOf(Key(0, 9)));
            Assert.Equal(2, log.IndexOf(Key(1, 0)));
            Assert.Equal(-1, log.IndexOf(Key(0, 8)));
            log.Clear();
            Assert.Equal(-1, log.IndexOf(Key(0, 7)));
        }

        [Fact]
        public void BackstopShedsOldestCyclesWholeAndCounts()
        {
            var log = new ExecutionLog(maxEntries: 100);
            for (int c = 0; c < 60; c++)
                for (int i = 0; i < 2; i++) log.Add(0, c, "x");
            // 120 entries crossed 100: the drop sheds oldest whole cycles to ~95.
            Assert.True(log.EntryCount <= 100);
            Assert.True(log.DroppedCycles > 0);
            Assert.Equal(-1, log.IndexOf(Key(0, 0)));
            Assert.Empty(log.Entries(Key(0, 0)));
            // The survivors are contiguous newest cycles with a consistent index.
            var first = log.Cycles[0];
            Assert.Equal(60 - log.Cycles.Count, first.Cycle);
            for (int i = 0; i < log.Cycles.Count; i++)
                Assert.Equal(i, log.IndexOf(log.Cycles[i]));
            Assert.Equal(Key(0, 59), log.Cycles[log.Cycles.Count - 1]);
        }

        [Fact]
        public void BackstopNeverShedsTheNewestCycle()
        {
            var log = new ExecutionLog(maxEntries: 5);
            for (int i = 0; i < 40; i++) log.Add(0, 3, "e" + i); // one giant cycle
            Assert.Equal(new[] { Key(0, 3) }, log.Cycles);
            Assert.Equal(40, log.Entries(Key(0, 3)).Count);
        }

        [Fact]
        public void ClearEmpties()
        {
            var log = new ExecutionLog();
            log.Add(0, 1, "x");
            log.Clear();
            Assert.True(log.IsEmpty);
            Assert.Equal(0, log.EntryCount);
            Assert.Empty(log.Cycles);
            Assert.Empty(log.Entries(Key(0, 1)));
        }
    }
}
