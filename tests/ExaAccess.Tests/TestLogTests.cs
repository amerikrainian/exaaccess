using ExaAccess.UI;
using Xunit;

namespace ExaAccess.Tests
{
    /// <summary>The editor's per-run test log: events grouped by test in arrival order, capped
    /// by newest tests and by entries per test (overflow counted, not kept).</summary>
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
            Assert.Equal(new[] { 2, 0 }, log.Tests);
            Assert.Equal(new[] { "c1", "c2" }, log.Entries(2));
            Assert.Equal(new[] { "a1", "a2" }, log.Entries(0));
            Assert.Empty(log.Entries(5));
            Assert.Equal(0, log.Overflow(2));
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
        public void CapsEntriesPerTestAndCountsOverflow()
        {
            var log = new TestLog();
            for (int i = 0; i < TestLog.MaxPerTest + 7; i++) log.Add(3, "e" + i);
            Assert.Equal(TestLog.MaxPerTest, log.Entries(3).Count);
            Assert.Equal("e0", log.Entries(3)[0]);
            Assert.Equal(7, log.Overflow(3));
        }

        [Fact]
        public void DropsOldestTestsWhole()
        {
            var log = new TestLog();
            for (int t = 0; t < TestLog.MaxTests + 2; t++) log.Add(t, "t" + t);
            Assert.Equal(TestLog.MaxTests, log.Tests.Count);
            Assert.Equal(2, log.Tests[0]);
            Assert.Empty(log.Entries(0));
            Assert.Equal(0, log.Overflow(0));
            Assert.Equal(new[] { "t2" }, log.Entries(2));
        }

        [Fact]
        public void ClearEmpties()
        {
            var log = new TestLog();
            log.Add(1, "x");
            log.Clear();
            Assert.True(log.IsEmpty);
            Assert.Empty(log.Tests);
            Assert.Empty(log.Entries(1));
        }
    }
}
