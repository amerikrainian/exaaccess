using System;
using ExaAccess.Localization;
using ExaAccess.Modularity;
using HarmonyLib;

namespace ExaAccess
{
    /// <summary>
    /// The module entry point — what the host's ModuleLoader instantiates. Composition root for all
    /// reloadable features: localization first (everything speakable depends on it), then the module's
    /// own Harmony patches (per-load unique id — see IModModule), announcer hooks, and FrameLoop steps.
    /// All statics in this assembly are per-load — a reload starts this whole half of the mod cold,
    /// re-deriving everything from the live game (and re-reading the locale files).
    /// </summary>
    public sealed class ExaAccessModule : IModModule
    {
        private ModHost _host;
        private Harmony _harmony;

        public void Load(ModHost host)
        {
            _host = host;
            LocalizationManager.Initialize();

            // The announcer's localized wording hooks (the graph core itself never touches Loc).
            UI.Graph.GraphAnnouncer.PositionText = (index, count) => Loc.T("nav.position", new { index, count });
            UI.Graph.GraphAnnouncer.ExpandedStateText = e => Loc.T(e ? "role.expanded" : "role.collapsed");

            // Module-owned patches: per-load UNIQUE id so THIS load's Dispose unpatches exactly these
            // (the host loads the new module before disposing the old — a fixed id would let the old
            // teardown strip the fresh patches; see IModModule).
            _harmony = new Harmony("com.exaaccess.module." + Guid.NewGuid().ToString("N"));
            if (!host.GameInitialized)
                Patches.SplashPatches.Apply(_harmony); // pre-init only: the splash is long gone on a reload
            Patches.GameKeySuppression.Apply(_harmony); // focus-mode key swallow (see the class doc)

            RegisterInput();
            Screens.ScreenManager.Initialize();

            // Per-frame steps, in the order they run: one keyboard snapshot, then input dispatch, then
            // the screen stack (which drives the navigator). (The dev pump is host-side, first.)
            FrameLoop.Register("keyboard", Input.SdlKeyboard.Update);
            FrameLoop.Register("input", Input.InputManager.Tick);
            FrameLoop.Register("loc", LocalizationManager.Tick);
            FrameLoop.Register("screens", Screens.ScreenManager.Tick);

            // One greeting, right at boot (the game window isn't even up yet), so the user knows the
            // mod is alive before the splash prompt arrives. A hot reload mid-session doesn't re-greet.
            if (host.ModuleGeneration == 1)
                Speech.Tts.Speak(Loc.T("app.ready"));
        }

        /// <summary>The nav action set (the WrathAccess ui.* vocabulary GraphNavigator dispatches on)
        /// and the input hooks. Bindings are the standard screen-reader set; rebinding UI comes with
        /// the settings tree.</summary>
        private static void RegisterInput()
        {
            Input.InputManager.Register("ui.up", "Up", Input.InputCategory.UI).AddBinding(Input.Scancode.Up).Repeating();
            Input.InputManager.Register("ui.down", "Down", Input.InputCategory.UI).AddBinding(Input.Scancode.Down).Repeating();
            Input.InputManager.Register("ui.left", "Left", Input.InputCategory.UI).AddBinding(Input.Scancode.Left).Repeating();
            Input.InputManager.Register("ui.right", "Right", Input.InputCategory.UI).AddBinding(Input.Scancode.Right).Repeating();
            Input.InputManager.Register("ui.next", "Next group", Input.InputCategory.UI).AddBinding(Input.Scancode.Tab).Repeating();
            Input.InputManager.Register("ui.prev", "Previous group", Input.InputCategory.UI).AddBinding(Input.Scancode.Tab, shift: true).Repeating();
            Input.InputManager.Register("ui.home", "First item", Input.InputCategory.UI).AddBinding(Input.Scancode.Home);
            Input.InputManager.Register("ui.end", "Last item", Input.InputCategory.UI).AddBinding(Input.Scancode.End);
            Input.InputManager.Register("ui.activate", "Activate", Input.InputCategory.UI)
                .AddBinding(Input.Scancode.Return).AddBinding(Input.Scancode.KpEnter);
            Input.InputManager.Register("ui.secondary", "Secondary action", Input.InputCategory.UI).AddBinding(Input.Scancode.Backspace);
            Input.InputManager.Register("ui.tooltip", "Read details", Input.InputCategory.UI).AddBinding(Input.Scancode.Space);
            Input.InputManager.Register("ui.back", "Back", Input.InputCategory.UI).AddBinding(Input.Scancode.Escape);
            Input.InputManager.Register("ui.regionPrev", "Previous region", Input.InputCategory.UI).AddBinding(Input.Scancode.Up, ctrl: true).Repeating();
            Input.InputManager.Register("ui.regionNext", "Next region", Input.InputCategory.UI).AddBinding(Input.Scancode.Down, ctrl: true).Repeating();

            Input.InputManager.ActiveCategoriesProvider = () =>
                new System.Collections.Generic.List<Input.InputCategory>(Screens.ScreenManager.ActiveInputCategories());
            Input.InputManager.UiDispatcher = UI.Navigation.DispatchJustPressed;
            Input.InputManager.SuppressPoll = () =>
            {
                var cur = Screens.ScreenManager.Current;
                return cur != null && cur.CapturesRawInput;
            };
        }

        public void Tick() => FrameLoop.Tick();

        public void Dispose()
        {
            // This Harmony build predates UnpatchSelf; UnpatchAll(ownId) is the same owner-scoped removal.
            try { _harmony?.UnpatchAll(_harmony.Id); }
            catch (Exception ex) { Log.Error("[module] UnpatchSelf failed", ex); }
            _harmony = null;
        }
    }
}
