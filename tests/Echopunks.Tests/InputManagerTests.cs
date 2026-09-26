using System;
using System.Collections.Generic;
using Echopunks.Input;
using Xunit;

namespace Echopunks.Tests
{
    public class InputManagerTests : IDisposable
    {
        /// <summary>A binding driven by test flags instead of the SDL keyboard.</summary>
        private sealed class FakeBinding : InputBinding
        {
            public bool Pressed;
            public bool IsHeld;
            private readonly string _id;

            public FakeBinding(string id) { _id = id; }

            public override string DisplayName => _id;
            public override bool JustPressed() => Pressed;
            public override bool Held() => IsHeld;
            public override bool Released() => false;
            public override string Type => "fake";
            public override string Serialize() => _id;
        }

        public InputManagerTests() { InputManager.ResetForTests(); }
        public void Dispose() { InputManager.ResetForTests(); }

        [Fact]
        public void GlobalActionsFireWithoutAnyProvider()
        {
            int fired = 0;
            var b = new FakeBinding("g");
            InputManager.Register("test.global", "Test", InputCategory.Global, () => fired++).AddBinding(b);

            b.Pressed = b.IsHeld = true;
            InputManager.Tick();
            Assert.Equal(1, fired);

            b.Pressed = false; // still held, not repeating — no second fire
            InputManager.Tick();
            Assert.Equal(1, fired);
        }

        [Fact]
        public void UiActionsAreDeadWithoutTheirCategory()
        {
            int fired = 0;
            var b = new FakeBinding("u");
            InputManager.Register("test.ui", "Test", InputCategory.UI, () => fired++).AddBinding(b);

            b.Pressed = b.IsHeld = true;
            InputManager.Tick(); // UI not live (no provider) — binding is not live
            Assert.Equal(0, fired);

            InputManager.ActiveCategoriesProvider = () => new List<InputCategory> { InputCategory.UI };
            InputManager.Tick();
            Assert.Equal(1, fired);
        }

        [Fact]
        public void UiDispatchConsumptionSuppressesPerformed()
        {
            int fired = 0;
            var b = new FakeBinding("u");
            InputManager.Register("ui.down", "Down", InputCategory.UI, () => fired++).AddBinding(b);
            InputManager.ActiveCategoriesProvider = () => new List<InputCategory> { InputCategory.UI };
            InputManager.UiDispatcher = a => a.Key == "ui.down"; // navigator consumes

            b.Pressed = b.IsHeld = true;
            InputManager.Tick();
            Assert.Equal(0, fired); // consumed, so Performed never fired
        }

        [Fact]
        public void IdenticalChordIsShadowedByTheHigherCategory()
        {
            int uiFired = 0, globalFired = 0;
            var uiB = new FakeBinding("same-chord");
            var glB = new FakeBinding("same-chord"); // identical serialized chord
            InputManager.Register("test.ui", "UI", InputCategory.UI, () => uiFired++).AddBinding(uiB);
            InputManager.Register("test.global", "Global", InputCategory.Global, () => globalFired++).AddBinding(glB);
            InputManager.ActiveCategoriesProvider = () => new List<InputCategory> { InputCategory.UI };

            uiB.Pressed = uiB.IsHeld = true;
            glB.Pressed = glB.IsHeld = true;
            InputManager.Tick();
            Assert.Equal(1, uiFired);     // UI ranked first (provider order) — owns the chord
            Assert.Equal(0, globalFired); // the global twin is shadowed
        }

        [Fact]
        public void SuppressPollStandsDownEntirely()
        {
            int fired = 0;
            var b = new FakeBinding("g");
            InputManager.Register("test.global", "Test", InputCategory.Global, () => fired++).AddBinding(b);
            InputManager.SuppressPoll = () => true;

            b.Pressed = b.IsHeld = true;
            InputManager.Tick();
            Assert.Equal(0, fired);
        }
    }

    public class SdlKeyboardBindingTests
    {
        [Fact]
        public void SerializationRoundTrips()
        {
            var b = new SdlKeyboardBinding(Scancode.A, ctrl: true, shift: true);
            var back = SdlKeyboardBinding.Deserialize(b.Serialize());
            Assert.Equal(Scancode.A, back.Key);
            Assert.True(back.Ctrl);
            Assert.True(back.Shift);
            Assert.False(back.Alt);
            Assert.Equal(b.Chord, back.Chord);
        }

        [Fact]
        public void DeserializeRejectsGarbage()
        {
            Assert.Null(SdlKeyboardBinding.Deserialize("NotAKey|ctrl"));
        }

        [Fact]
        public void DisplayNameSpellsTheChord()
        {
            Assert.Equal("Ctrl+Shift+A", new SdlKeyboardBinding(Scancode.A, ctrl: true, shift: true).DisplayName);
            Assert.Equal("Down", new SdlKeyboardBinding(Scancode.Down).DisplayName);
        }

        [Fact]
        public void BindingDeserializerRoutesByType()
        {
            var b = InputBinding.Deserialize("keyboard", "Space|");
            Assert.IsType<SdlKeyboardBinding>(b);
            Assert.Null(InputBinding.Deserialize("gamepad", "x"));
        }
    }
}
