using ExaAccess.UI;
using Xunit;

namespace ExaAccess.Tests
{
    public class ScreenNamesTests
    {
        [Theory]
        [InlineData("DesktopScreen", "Desktop")]
        [InlineData("EditorScreen", "Editor")]
        [InlineData("ControlPanelScreen", "Control panel")]
        [InlineData("GifRecorderScreen", "GIF recorder")]
        public void KnownScreensGetCuratedLabels(string typeName, string expected)
        {
            Assert.Equal(expected, ScreenNames.Friendly(typeName));
        }

        [Fact]
        public void UnknownScreensDropSuffixAndDeCamel()
        {
            Assert.Equal("Highscore Browser", ScreenNames.Friendly("HighscoreBrowserScreen"));
        }

        [Fact]
        public void NonScreenTypeNameIsJustDeCameled()
        {
            Assert.Equal("Pause Menu", ScreenNames.Friendly("PauseMenu"));
        }

        [Theory]
        [InlineData("#=qzDwg_bRKgBybN2rYiyLHng==")]
        [InlineData("Has Space")]
        [InlineData("")]
        [InlineData(null)]
        public void ObfuscatedOrInvalidNamesAreDetected(string name)
        {
            Assert.True(ScreenNames.IsObfuscated(name));
        }

        [Theory]
        [InlineData("DesktopScreen")]
        [InlineData("Class5_0")]
        public void CleanIdentifiersAreNotObfuscated(string name)
        {
            Assert.False(ScreenNames.IsObfuscated(name));
        }

        [Fact]
        public void DeCamelSplitsOnlyLowerToUpperBoundaries()
        {
            Assert.Equal("Custom Puzzle", ScreenNames.DeCamel("CustomPuzzle"));
            // An acronym run does NOT get split after itself ("GIFRecorder" stays glued) — which is
            // precisely why all-caps screens like GifRecorderScreen carry curated labels instead.
            Assert.Equal("GIFRecorder", ScreenNames.DeCamel("GIFRecorder"));
        }
    }
}
