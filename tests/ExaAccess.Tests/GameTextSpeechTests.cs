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
        public void KeepsEscapedLiterals()
        {
            Assert.Equal("HACK*MATCH", GameText.Speech("HACK\\*MATCH"));
        }

        [Fact]
        public void MassagesSeparatorsAndNewlines()
        {
            Assert.Equal("Reset, Pause", GameText.Speech("Reset / Pause"));
            Assert.Equal("a b", GameText.Speech("a\nb"));
        }
    }
}
