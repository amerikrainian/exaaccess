using System;
using System.Collections.Generic;

namespace ExaAccess.Input
{
    /// <summary>
    /// Registry + per-frame poll (ported from WrathAccess), ticked from the module's FrameLoop after
    /// <see cref="SdlKeyboard.Update"/>. Actions live in CATEGORIES: each frame the live categories are
    /// whatever <see cref="ActiveCategoriesProvider"/> reports (priority order — the screen stack will
    /// plug in here when screens land), plus Global, which is always on. An identical chord in two live
    /// categories resolves to the higher-priority one (shadowing). UI-category presses dispatch into
    /// <see cref="UiDispatcher"/> (the future navigator); everything else fires its handler directly.
    /// Typematic repeat follows the user's own OS keyboard delay/rate.
    ///
    /// WrathAccess's suppression gates (game text field, raw-capture screens, the game's own-keys
    /// mute) plug in via <see cref="SuppressPoll"/> once the corresponding EXAPUNKS hooks exist.
    /// </summary>
    public static class InputManager
    {
        private static readonly List<InputAction> _actions = new List<InputAction>();
        public static IReadOnlyList<InputAction> Actions => _actions;

        /// <summary>Live categories beyond Global, priority order (the screen stack, later). Null = none.</summary>
        public static Func<IReadOnlyList<InputCategory>> ActiveCategoriesProvider;

        /// <summary>Consumes a UI-category press (the navigator). Null / false = not consumed.</summary>
        public static Func<InputAction, bool> UiDispatcher;

        /// <summary>Return true to stand down entirely this frame (typing in a game text field, a
        /// raw-capture screen). Null = never suppressed.</summary>
        public static Func<bool> SuppressPoll;

        public static InputAction Register(string key, string label, InputCategory category, Action onPerformed = null)
        {
            var action = new InputAction(key, label) { Category = category };
            if (onPerformed != null) action.Performed += onPerformed;
            _actions.Add(action);
            return action;
        }

        // The frame's live state, rebuilt at the top of Tick (cheap: few actions x ~1 binding).
        private static readonly List<InputCategory> _activeCats = new List<InputCategory>();
        private static readonly HashSet<InputBinding> _live = new HashSet<InputBinding>();
        private static readonly Dictionary<string, int> _chordRank = new Dictionary<string, int>();

        /// <summary>Whether the action with this key is currently held via a LIVE (unshadowed,
        /// active-category) binding — for per-frame polling consumers.</summary>
        public static bool Held(string key)
        {
            for (int i = 0; i < _actions.Count; i++)
                if (_actions[i].Key == key) return HeldLive(_actions[i]);
            return false;
        }

        private static bool JustPressedLive(InputAction a)
        {
            for (int i = 0; i < a.Bindings.Count; i++)
                if (_live.Contains(a.Bindings[i]) && a.Bindings[i].JustPressed()) return true;
            return false;
        }

        private static bool HeldLive(InputAction a)
        {
            for (int i = 0; i < a.Bindings.Count; i++)
                if (_live.Contains(a.Bindings[i]) && a.Bindings[i].Held()) return true;
            return false;
        }

        // Live categories = the provider's list + Global. Then walk categories in priority order marking
        // bindings live, shadowing any identical chord already claimed by an earlier (higher-priority)
        // category. Same-category duplicates are both live.
        private static void RebuildLive()
        {
            _activeCats.Clear();
            var provided = ActiveCategoriesProvider?.Invoke();
            if (provided != null)
                foreach (var c in provided)
                    if (!_activeCats.Contains(c)) _activeCats.Add(c);
            if (!_activeCats.Contains(InputCategory.Global)) _activeCats.Add(InputCategory.Global);

            _live.Clear();
            _chordRank.Clear();
            for (int rank = 0; rank < _activeCats.Count; rank++)
            {
                var cat = _activeCats[rank];
                for (int i = 0; i < _actions.Count; i++)
                {
                    var a = _actions[i];
                    if (a.Category != cat) continue;
                    for (int j = 0; j < a.Bindings.Count; j++)
                    {
                        var b = a.Bindings[j];
                        var chord = b.Chord; // cached per binding (the WotR per-frame GC lesson)
                        if (_chordRank.TryGetValue(chord, out int owner))
                        {
                            if (owner < rank) continue; // shadowed by a higher category
                        }
                        else _chordRank[chord] = rank;
                        _live.Add(b);
                    }
                }
            }
        }

        public static void Tick()
        {
            if (SuppressPoll != null && SuppressPoll()) return;

            RebuildLive(); // this frame's category claims + chord shadowing

            // Typematic repeat: fire once, pause, then repeat while held — at the user's
            // own OS keyboard delay/rate.
            float now = FrameClock.Now;
            float initialDelay = OsKeyboard.InitialDelay;
            float repeatInterval = OsKeyboard.RepeatInterval;
            for (int i = 0; i < _actions.Count; i++)
            {
                var action = _actions[i];
                bool held = HeldLive(action);

                bool fire = false;
                if (JustPressedLive(action))
                {
                    fire = true;
                    action.NextRepeatTime = now + initialDelay;
                }
                else if (action.Repeats && held && action.NextRepeatTime > 0f && now >= action.NextRepeatTime)
                {
                    // Held past the delay → auto-repeat, at most one step per frame. The
                    // NextRepeatTime > 0 guard means we only repeat an action that was actually
                    // JustPressed this hold — NOT one that just became held because a shared key's
                    // modifier was released (releasing Shift while holding Tab must not fire a
                    // stray forward Tab).
                    fire = true;
                    action.NextRepeatTime = now + repeatInterval;
                }
                if (!held) action.NextRepeatTime = 0f; // reset on release (disarms repeat until next press)

                if (!fire) continue;
                bool consumed = action.Category == InputCategory.UI
                    && UiDispatcher != null && UiDispatcher(action);
                if (!consumed) action.InvokePerformed();
            }
        }

        /// <summary>Test seam: forget every registered action and hook.</summary>
        internal static void ResetForTests()
        {
            _actions.Clear();
            _activeCats.Clear();
            _live.Clear();
            _chordRank.Clear();
            ActiveCategoriesProvider = null;
            UiDispatcher = null;
            SuppressPoll = null;
        }
    }
}
