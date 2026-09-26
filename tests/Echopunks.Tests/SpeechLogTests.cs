#if DEBUG
using Echopunks.Dev;
using Xunit;

namespace Echopunks.Tests
{
    /// <summary>SpeechLog is DEBUG-only in the mod, so these tests exist only in Debug test runs
    /// (the default for dotnet test) — same shape as the dev server that consumes it.</summary>
    public class SpeechLogTests
    {
        [Fact]
        public void CursorAdvancesAndOnlyNewLinesComeBack()
        {
            var log = new SpeechLog();
            log.Add("first");
            log.Add("second");

            long next;
            string all = log.Render(0, out next);
            Assert.Equal("0: first\n1: second\n", all);
            Assert.Equal(2, next);

            log.Add("third");
            string tail = log.Render(next, out next);
            Assert.Equal("2: third\n", tail);
            Assert.Equal(3, next);
        }

        [Fact]
        public void EmptyAndNullAddsAreIgnored()
        {
            var log = new SpeechLog();
            log.Add(null);
            log.Add("");
            long next;
            Assert.Equal("", log.Render(0, out next));
            Assert.Equal(0, next);
        }

        [Fact]
        public void EvictionKeepsGlobalIndicesStable()
        {
            var log = new SpeechLog();
            for (int i = 0; i <= 500; i++) log.Add("line" + i); // capacity 500 -> "line0" evicted

            long next;
            string s = log.Render(0, out next);
            Assert.StartsWith("1: line1\n", s);   // index 0 is gone but indices did not shift
            Assert.Equal(501, next);

            // A cursor pointing into the evicted range clamps to the oldest retained line.
            string clamped = log.Render(0, out next);
            Assert.StartsWith("1: line1\n", clamped);
        }
    }
}
#endif
