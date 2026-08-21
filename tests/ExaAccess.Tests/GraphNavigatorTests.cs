using System;
using System.Collections.Generic;
using ExaAccess.Input;
using ExaAccess.Screens;
using ExaAccess.UI;
using ExaAccess.UI.Graph;
using Xunit;

namespace ExaAccess.Tests
{
    public class GraphNavigatorTests : IDisposable
    {
        private sealed class TestScreen : Screen
        {
            public Action<GraphBuilder> Declare;
            public override string Key => "test";
            public override bool IsActive() => true;
            public override void Build(GraphBuilder b) => Declare?.Invoke(b);
        }

        private readonly TestSpeech _speech = new TestSpeech();
        private readonly GraphNavigator _nav = new GraphNavigator();

        public GraphNavigatorTests() { FocusMode.Active = true; }

        public void Dispose()
        {
            FocusMode.Active = true;
            _speech.Dispose();
        }

        private static NodeVtable Vt(string label, Action activate = null, Func<string> state = null)
            => new NodeVtable
            {
                Announcements = new[] { NodeAnnouncement.Static(label) },
                OnActivate = activate,
                StateText = state,
            };

        private TestScreen TwoItemScreen()
            => new TestScreen
            {
                Declare = b =>
                {
                    b.AddItem(ControlId.Structural("a"), Vt("Alpha"));
                    b.AddItem(ControlId.Structural("b"), Vt("Beta"));
                },
            };

        private static InputAction Action(string key) => new InputAction(key, key);

        [Fact]
        public void EnsureFocusSeatsAndAnnouncesEntryExactlyOnce()
        {
            _nav.Attach(TwoItemScreen());
            _nav.EnsureFocus();
            Assert.Equal(new[] { "Alpha" }, _speech.Spoken);

            _nav.EnsureFocus(); // no change — no re-announce
            Assert.Equal(new[] { "Alpha" }, _speech.Spoken);
            Assert.True(_nav.HasFocus);
        }

        [Fact]
        public void ArrowMovesAndAnnounces()
        {
            _nav.Attach(TwoItemScreen());
            _nav.EnsureFocus();

            Assert.True(_nav.OnInputJustPressed(Action("ui.down")));
            Assert.Equal(new[] { "Alpha", "Beta" }, _speech.Spoken);

            // At the bottom edge of a plain list: not moved, bubbles (returns false).
            Assert.False(_nav.OnInputJustPressed(Action("ui.down")));
            Assert.Equal(2, _speech.Spoken.Count);
        }

        [Fact]
        public void ActivateRunsTheVtableAndSpeaksStateText()
        {
            bool clicked = false;
            var screen = new TestScreen
            {
                Declare = b => b.AddItem(ControlId.Structural("t"),
                    Vt("Toggle", activate: () => clicked = true, state: () => "on")),
            };
            _nav.Attach(screen);
            _nav.EnsureFocus();

            Assert.True(_nav.OnInputJustPressed(Action("ui.activate")));
            Assert.True(clicked);
            Assert.Contains("on", _speech.Spoken);
        }

        [Fact]
        public void ReattachingADifferentScreenResetsAndRestoringKeepsFocus()
        {
            var a = TwoItemScreen();
            var b = new TestScreen { Declare = gb => gb.AddItem(ControlId.Structural("x"), Vt("Xray")) };

            _nav.Attach(a);
            _nav.EnsureFocus();
            _nav.OnInputJustPressed(Action("ui.down")); // focus Beta

            _nav.Attach(b);
            _nav.EnsureFocus(); // announces Xray

            _nav.Attach(a); // back: per-screen state restores focus on Beta
            _nav.EnsureFocus();
            Assert.Equal("Beta", _speech.Spoken[_speech.Spoken.Count - 1]);
        }

        [Fact]
        public void ScreenClosedDropsPerScreenState()
        {
            var a = TwoItemScreen();
            _nav.Attach(a);
            _nav.EnsureFocus();
            _nav.OnInputJustPressed(Action("ui.down")); // Beta

            _nav.ScreenClosed(a);
            _nav.Attach(new TestScreen { Declare = gb => gb.AddItem(ControlId.Structural("x"), Vt("Xray")) });
            _nav.Attach(a); // fresh state → back to the start node
            _nav.EnsureFocus();
            Assert.Equal("Alpha", _speech.Spoken[_speech.Spoken.Count - 1]);
        }

        [Fact]
        public void LivePartsSpeakOnChangeOnly()
        {
            string value = "off";
            var screen = new TestScreen
            {
                Declare = b => b.AddItem(ControlId.Structural("t"), new NodeVtable
                {
                    Announcements = new[]
                    {
                        NodeAnnouncement.Static("Power"),
                        new NodeAnnouncement(() => value, live: true),
                    },
                }),
            };
            _nav.Attach(screen);
            _nav.EnsureFocus(); // baseline (entry announce includes "off")
            int after = _speech.Spoken.Count;

            _nav.EnsureFocus(); // unchanged — silent
            Assert.Equal(after, _speech.Spoken.Count);

            value = "on";
            _nav.EnsureFocus();
            Assert.Equal("on", _speech.Spoken[_speech.Spoken.Count - 1]);
        }

        [Fact]
        public void FocusModeOffSilencesTheDiffer()
        {
            FocusMode.Active = false;
            _nav.Attach(TwoItemScreen());
            _nav.EnsureFocus();
            Assert.Empty(_speech.Spoken);
        }

        [Fact]
        public void TabCyclesStopsAndConsumesAtTheEnd()
        {
            var screen = new TestScreen
            {
                Declare = b =>
                {
                    b.AddItem(ControlId.Structural("a"), Vt("Alpha"));
                    b.BeginStop();
                    b.AddItem(ControlId.Structural("b"), Vt("Beta"));
                },
            };
            _nav.Attach(screen);
            _nav.EnsureFocus();

            Assert.True(_nav.OnInputJustPressed(Action("ui.next")));
            Assert.Equal("Beta", _speech.Spoken[_speech.Spoken.Count - 1]);

            Assert.True(_nav.OnInputJustPressed(Action("ui.next"))); // at the last stop: consume, no wrap
            Assert.Equal("Beta", _speech.Spoken[_speech.Spoken.Count - 1]);
        }
    }
}
