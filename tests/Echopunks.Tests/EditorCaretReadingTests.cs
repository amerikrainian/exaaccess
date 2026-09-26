using Echopunks.UI;
using Xunit;

namespace Echopunks.Tests
{
    public class EditorCaretReadingTests
    {
        [Fact]
        public void WordJumpReadsTheWholeWordLandedOn()
        {
            Assert.Equal("800", CaretText.WordAt("LINK 800\nGRAB 200", 5));
            Assert.Equal("LINK", CaretText.WordAt("LINK 800", 0));
            Assert.Equal("GRAB", CaretText.WordAt("LINK 800\nGRAB 200", 9));
        }

        [Fact]
        public void LineIndexCountsNewlinesBeforeTheCaret()
        {
            Assert.Equal(0, CaretText.LineIndex("LINK 800\nGRAB 200", 4));
            Assert.Equal(1, CaretText.LineIndex("LINK 800\nGRAB 200", 9));
        }

        [Fact]
        public void OffsetOfLineIsTheLineStartAndClampsPastTheEnd()
        {
            Assert.Equal(0, CaretText.OffsetOfLine("LINK 800\nGRAB 200", 0));
            Assert.Equal(9, CaretText.OffsetOfLine("LINK 800\nGRAB 200", 1));
            Assert.Equal(9, CaretText.OffsetOfLine("LINK 800\nGRAB 200", 5)); // clamp: last line's start
            Assert.Equal(0, CaretText.OffsetOfLine("", 3));
        }
    }
}
