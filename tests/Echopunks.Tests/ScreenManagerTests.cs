using System;
using System.IO;
using Echopunks.Localization;
using Echopunks.Screens;
using Echopunks.UI;
using Echopunks.UI.Graph;
using Xunit;

namespace Echopunks.Tests
{
    public class ScreenManagerTests : IDisposable
    {
        private sealed class FakeScreen : Screen
        {
            private readonly string _key;
            public bool Active;
            public int Pushed, Popped, Focused, Unfocused;
            public string Name;

            public FakeScreen(string key, int layer = 0) { _key = key; LayerValue = layer; }
            public int LayerValue;
            public override string Key => _key;
            public override int Layer => LayerValue;
            public override string ScreenName => Name;
            public override bool IsActive() => Active;
            public override void Build(GraphBuilder b)
                => b.AddItem(ControlId.Structural(_key + ".item"), new NodeVtable
                {
                    Announcements = new[] { NodeAnnouncement.Static(_key + " item") },
                });
            public override void OnPush() { Pushed++; }
            public override void OnPop() { Popped++; }
            public override void OnFocus() { Focused++; base.OnFocus(); }
            public override void OnUnfocus() { Unfocused++; }
        }

        private readonly TestSpeech _speech = new TestSpeech();

        public ScreenManagerTests()
        {
            FocusMode.Active = true;
            ScreenManager.ResetForTests();
            Navigation.Active = new GraphNavigator();
            ScreenManager.GameTopScreenName = () => null;
            LocalizationManager.LanguageSource = null;
            LocalizationManager.Initialize(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "assets", "locale"));
        }

        public void Dispose()
        {
            ScreenManager.ResetForTests();
            Navigation.Active = new GraphNavigator();
            _speech.Dispose();
        }

        [Fact]
        public void ActivationPushesFocusesAndAnnouncesScreenThenItem()
        {
            var s = new FakeScreen("alpha") { Active = true, Name = "Alpha screen" };
            ScreenManager.Register(s);

            ScreenManager.Tick();
            Assert.Equal(1, s.Pushed);
            Assert.Equal(1, s.Focused);
            Assert.Same(s, ScreenManager.Current);
            // Screen name first (OnFocus), then the navigator's entry readout.
            Assert.Equal(new[] { "Alpha screen", "alpha item" }, _speech.Spoken);

            ScreenManager.Tick(); // steady state — nothing new
            Assert.Equal(2, _speech.Spoken.Count);
        }

        [Fact]
        public void DeactivationPopsAndDropsNavState()
        {
            var s = new FakeScreen("alpha") { Active = true };
            ScreenManager.Register(s);
            ScreenManager.Tick();

            s.Active = false;
            ScreenManager.Tick();
            Assert.Equal(1, s.Popped);
            Assert.Null(ScreenManager.Current);
        }

        [Fact]
        public void HigherLayerTakesFocusAndReturnRestores()
        {
            var baseScreen = new FakeScreen("base") { Active = true };
            var overlay = new FakeScreen("overlay", layer: 10) { Active = false };
            ScreenManager.Register(baseScreen);
            ScreenManager.Register(overlay);

            ScreenManager.Tick();
            Assert.Same(baseScreen, ScreenManager.Current);

            overlay.Active = true;
            ScreenManager.Tick();
            Assert.Same(overlay, ScreenManager.Current);
            Assert.Equal(1, baseScreen.Unfocused);
            Assert.Equal(0, baseScreen.Popped); // covered, not closed

            overlay.Active = false;
            ScreenManager.Tick();
            Assert.Same(baseScreen, ScreenManager.Current);
            Assert.Equal(2, baseScreen.Focused);
        }

        [Fact]
        public void UnmodeledGameScreensAnnounceByFriendlyNameOnceEach()
        {
            string top = "DesktopScreen";
            ScreenManager.GameTopScreenName = () => top;

            ScreenManager.Tick();
            Assert.Equal(new[] { "Desktop" }, _speech.Spoken);

            ScreenManager.Tick(); // unchanged — silent
            Assert.Single(_speech.Spoken);

            top = "EditorScreen";
            ScreenManager.Tick();
            Assert.Equal(new[] { "Desktop", "Editor" }, _speech.Spoken);
        }

        [Fact]
        public void ObfuscatedGameScreensAreNeverSpoken()
        {
            ScreenManager.GameTopScreenName = () => "#=qzDwg==";
            ScreenManager.Tick();
            Assert.Empty(_speech.Spoken);
        }

        [Fact]
        public void ModeledScreenSuppressesTheFallbackAnnounce()
        {
            var s = new FakeScreen("alpha") { Active = true, Name = "Alpha screen" };
            ScreenManager.Register(s);
            ScreenManager.GameTopScreenName = () => "DesktopScreen"; // covered by the modeled screen

            ScreenManager.Tick();
            Assert.DoesNotContain("Desktop", _speech.Spoken);
        }

        [Fact]
        public void ActiveInputCategoriesFollowTheStackAndFocusMode()
        {
            var s = new FakeScreen("alpha") { Active = true };
            ScreenManager.Register(s);
            ScreenManager.Tick();

            Assert.Contains(Echopunks.Input.InputCategory.UI, ScreenManager.ActiveInputCategories());

            FocusMode.Active = false;
            Assert.Empty(ScreenManager.ActiveInputCategories());
            FocusMode.Active = true;
        }
    }
}
