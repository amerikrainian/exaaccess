using ExaAccess.Screens;
using Xunit;

namespace ExaAccess.Tests
{
    public class EditorCaretReadingTests
    {
        [Fact]
        public void WordJumpReadsTheWholeWordLandedOn()
        {
            Assert.Equal("800", ExaEditorScreen.WordAt("LINK 800\nGRAB 200", 5));
            Assert.Equal("LINK", ExaEditorScreen.WordAt("LINK 800", 0));
            Assert.Equal("GRAB", ExaEditorScreen.WordAt("LINK 800\nGRAB 200", 9));
        }

        [Fact]
        public void LineIndexCountsNewlinesBeforeTheCaret()
        {
            Assert.Equal(0, ExaEditorScreen.LineIndexForTest("LINK 800\nGRAB 200", 4));
            Assert.Equal(1, ExaEditorScreen.LineIndexForTest("LINK 800\nGRAB 200", 9));
        }
    }
}
