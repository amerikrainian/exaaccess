using ExaAccess.Game;
using Xunit;

namespace ExaAccess.Tests
{
    public class GameTextSpeechTests
    {
        [Fact]
        public void StripsEmphasisMarkup()
        {
            Assert.Equal("Move file 200 to the outbox.",
                GameText.Speech("Move file 200 to the *outbox*."));
            Assert.Equal("you must leave no trace.",
                GameText.Speech("you must _leave no trace_."));
        }

        [Fact]
        public void StripsKeywordMarkers()
        {
            Assert.Equal("Remove the keyword PEANUTS (file 300).",
                GameText.Speech("Remove the keyword ‗PEANUTS‗ (file 300)."));
        }

        [Fact]
        public void KeepsEscapedLiterals()
        {
            Assert.Equal("HACK*MATCH", GameText.Speech("HACK\\*MATCH"));
        }

        [Fact]
        public void MassagesSeparatorsAndNewlines()
        {
            // The only " / " in the game's text is PB023's division sign — it must survive.
            Assert.Equal("(BA + ZA + APB) / 3", GameText.Speech("(BA + ZA + APB) / 3"));
            Assert.Equal("a b", GameText.Speech("a\nb"));
        }
    }
}
