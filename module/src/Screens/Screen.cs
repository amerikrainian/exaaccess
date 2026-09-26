using System.Collections.Generic;
using Echopunks.UI;

namespace Echopunks.Screens
{
    /// <summary>
    /// Base for a navigable screen, ported from WrathAccess. Lifecycle (dispatched by ScreenManager
    /// from the stack diff): OnPush (entered the stack) → OnFocus (became active); OnUnfocus → OnPop
    /// on the way out. The push/focus split enables focus restoration: a covered screen gets OnUnfocus
    /// then, when re-exposed, OnFocus without another OnPush, so its remembered focus survives.
    ///
    /// Navigation/input is owned by the active Navigator (Navigation.Active), which ScreenManager
    /// attaches to the focused screen. Screens declare their graph (Build, IMMEDIATE MODE: called on
    /// every render — declare controls fresh from live game state each call; focus persists by
    /// ControlId identity) and expose ScreenName.
    /// </summary>
    public abstract class Screen
    {
        /// <summary>Whether Tab wraps from the last stop back to the first.</summary>
        public bool Wrap { get; set; }

        /// <summary>Screen-level actions (Back/Escape handlers and the like), dispatched by id.</summary>
        public virtual IEnumerable<ElementAction> GetActions() { yield break; }

        /// <summary>Find an advertised action by id and execute it. Returns true if found.</summary>
        public bool InvokeAction(string id)
        {
            foreach (var a in GetActions())
                if (a.Id == id) { a.Execute(); return true; }
            return false;
        }

        /// <summary>Stable identity used for stack diffing and logs.</summary>
        public abstract string Key { get; }

        /// <summary>The screen's graph declaration — immediate mode (see class doc). Declare nothing
        /// while the screen's content doesn't exist yet.</summary>
        public virtual void Build(UI.Graph.GraphBuilder b) { }

        /// <summary>Keep this screen's per-screen nav state (focus, stop memory, tree expansion) when
        /// it POPS off the stack. Default false: closing resets it — reopening starts fresh.</summary>
        public virtual bool KeepStateOnPop => false;

        /// <summary>The Tab-stop initial focus lands on when the screen first gains a cursor.
        /// Null = the graph's start node.</summary>
        public virtual object InitialFocusStop => null;

        /// <summary>Spoken when the screen gains focus. Null/empty = silent.</summary>
        public virtual string ScreenName => null;

        /// <summary>Stack layer: higher sits on top. 0 = base context, then windows, then overlays.</summary>
        public virtual int Layer => 0;

        /// <summary>Is this screen currently showing? Evaluated every frame (poll the game's own
        /// screen stack via GameState).</summary>
        public abstract bool IsActive();

        /// <summary>When true, the screen starts with NO focused element — input bubbles to global
        /// handlers, and Tab ENTERS the screen's content. Tabbing past the ends returns here.</summary>
        public virtual bool StartUnfocused => false;

        /// <summary>When true (and this is the focused screen), InputManager stops dispatching so raw
        /// keys reach the game (e.g. a key-binding capture dialog).</summary>
        public virtual bool CapturesRawInput => false;

        /// <summary>Keys (SDL keycodes) the game keeps even while this modeled screen is focused
        /// and suppression is active — for screens whose native shortcuts should coexist with the
        /// browse graph (the completion screen's Enter). Escape is never suppressed anyway.</summary>
        public virtual bool PassKeyToGame(int keycode) => false;

        /// <summary>True while a MOD-SIDE modal (a virtual popup with no game counterpart) is open
        /// on this screen: Escape then closes the modal (the screen's Back action) instead of
        /// reaching the game — the ONE exception to Escape never being suppressed. A game-backed
        /// overlay must never set this; the game's own Escape handling is its close path.</summary>
        public virtual bool ModalCapturesEscape => false;

        private static readonly Input.InputCategory[] UiOnly = { Input.InputCategory.UI };

        /// <summary>The input categories this screen uses while active, in priority order.</summary>
        public virtual IReadOnlyList<Input.InputCategory> InputCategories => UiOnly;

        /// <summary>When true, this screen blocks the input categories of screens BELOW it in the
        /// stack — a true modal that owns the keyboard.</summary>
        public virtual bool Exclusive => false;

        public virtual void OnPush() { }

        public virtual void OnFocus()
        {
            // Screen-change announcement (never interrupt — house preference). The Navigator
            // separately announces the focused element within the screen.
            if (!string.IsNullOrEmpty(ScreenName))
                Speech.Tts.Speak(ScreenName);
        }

        public virtual void OnUnfocus() { }
        public virtual void OnPop() { }

        /// <summary>Per-frame update for the ACTIVE (top) screen, dispatched by ScreenManager.</summary>
        public virtual void OnUpdate() { }

        // ---- child screen tree (mod-driven sub-screens within a screen) ----
        // A screen can host a single ActiveChild (which can host its own child, forming a chain) —
        // e.g. a dropdown's choice list or a confirm modal. The focused screen is the chain's deepest.
        // The poll-driven OUTER stack lives in ScreenManager; children are pushed/removed imperatively.

        /// <summary>The screen hosting this one as a child, or null for an outer screen.</summary>
        public Screen ParentScreen { get; private set; }

        /// <summary>This screen's single active child sub-screen, or null.</summary>
        public Screen ActiveChild { get; private set; }

        /// <summary>The deepest screen in this chain (this screen if it has no active child).</summary>
        public Screen DeepestActiveScreen()
        {
            var s = this;
            while (s.ActiveChild != null) s = s.ActiveChild;
            return s;
        }

        /// <summary>Push a sub-screen as this screen's active child (replacing any existing child).</summary>
        public void PushChild(Screen child)
        {
            if (child == null || child == ActiveChild) return;
            if (ActiveChild != null) RemoveChild(ActiveChild);
            child.ParentScreen = this;
            ActiveChild = child;
            child.OnPush();
        }

        /// <summary>Remove this screen's active child, disposing its whole subtree (deepest-first).</summary>
        public void RemoveChild(Screen child)
        {
            if (child == null || ActiveChild != child) return;
            if (child.ActiveChild != null) child.RemoveChild(child.ActiveChild);
            child.OnPop();
            Navigation.ScreenClosed(child);
            child.ParentScreen = null;
            ActiveChild = null;
        }
    }
}
