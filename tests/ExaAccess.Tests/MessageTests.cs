using System;
using ExaAccess.Localization;
using Xunit;

namespace ExaAccess.Tests
{
    public class MessageTests : IDisposable
    {
        public MessageTests() { Message.LocalizationResolver = null; }
        public void Dispose() { Message.LocalizationResolver = null; }

        [Fact]
        public void RawResolvesToItsText()
        {
            Assert.Equal("hello", Message.Raw("hello").Resolve());
        }

        [Fact]
        public void LocalizedWithoutResolverFallsBackToTheKey()
        {
            Assert.Equal("app.ready", Message.Localized("ui", "app.ready").Resolve());
        }

        [Fact]
        public void LocalizedUsesTheResolver()
        {
            Message.LocalizationResolver = (table, key) => table + ":" + key;
            Assert.Equal("ui:app.ready", Message.Localized("ui", "app.ready").Resolve());
        }

        [Fact]
        public void ResolverMissFallsBackToTheKey()
        {
            Message.LocalizationResolver = (table, key) => null;
            Assert.Equal("app.gone", Message.Localized("ui", "app.gone").Resolve());
        }

        [Fact]
        public void NamedVarsSubstitute()
        {
            var m = Message.Raw("{index} of {count}", new { index = 3, count = 10 });
            Assert.Equal("3 of 10", m.Resolve());
        }

        [Fact]
        public void MissingVarStaysLiteral()
        {
            var m = Message.Raw("{index} of {count}", new { index = 3 });
            Assert.Equal("3 of {count}", m.Resolve());
        }

        [Fact]
        public void MessageValuedVarsResolveLazilyAtSpeakTime()
        {
            Message.LocalizationResolver = (t, k) => "first";
            var m = Message.Raw("value: {v}", new { v = Message.Localized("ui", "x") });
            Message.LocalizationResolver = (t, k) => "second"; // e.g. a language switch before speaking
            Assert.Equal("value: second", m.Resolve());
        }

        [Fact]
        public void PlusJoinsWithSpacesSkippingEmpties()
        {
            var m = Message.Raw("Desktop") + Message.Empty + Message.Raw("3 of 10");
            Assert.Equal("Desktop 3 of 10", m.Resolve());
        }

        [Fact]
        public void JoinUsesTheSeparatorAndSkipsNulls()
        {
            var m = Message.Join(", ", Message.Raw("button"), null, Message.Raw("checked"));
            Assert.Equal("button, checked", m.Resolve());
        }

        [Fact]
        public void MaybeRawPassesNullThrough()
        {
            Assert.Null(Message.MaybeRaw(null));
            Assert.Equal("x", Message.MaybeRaw("x").Resolve());
        }

        [Fact]
        public void EmptyIsEmpty()
        {
            Assert.True(Message.Empty.IsEmpty);
            Assert.False(Message.Raw("x").IsEmpty);
        }
    }
}
