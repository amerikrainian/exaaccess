using Echopunks.Speech;
using Xunit;

namespace Echopunks.Tests
{
    public class TtsCleanTests
    {
        [Fact]
        public void CollapsesWhitespaceRunsAndControls()
        {
            Assert.Equal("one two three", Tts.Clean("one\n\ntwo\t  three"));
        }

        [Fact]
        public void TrimsEnds()
        {
            Assert.Equal("hello", Tts.Clean("  hello \r\n"));
        }

        [Fact]
        public void EmptyAndNullPassThrough()
        {
            Assert.Null(Tts.Clean(null));
            Assert.Equal("", Tts.Clean(""));
        }
    }
}
