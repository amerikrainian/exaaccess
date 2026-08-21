using ExaAccess.Input;
using ExaAccess.Screens;

namespace ExaAccess.UI
{
    /// <summary>
    /// Holds the active Navigator (swappable later) and is the entry point input dispatches into.
    /// ScreenManager re-attaches it on screen change. Ported from WrathAccess.
    /// </summary>
    public static class Navigation
    {
        public static Navigator Active = new GraphNavigator();

        public static void Attach(Screen screen) => Active?.Attach(screen);

        /// <summary>True when something is focused (the navigator owns the keys).</summary>
        public static bool HasFocus => Active != null && Active.HasFocus;

        public static bool DispatchJustPressed(InputAction action) =>
            Active != null && Active.OnInputJustPressed(action);

        public static void AnnounceCurrent() => Active?.AnnounceCurrent();

        /// <summary>Re-establish initial focus if the focused screen has focusable content but nothing
        /// is focused yet. Ticked each frame by ScreenManager.</summary>
        public static void EnsureFocus() => Active?.EnsureFocus();

        /// <summary>Return to the unfocused state — see <see cref="Navigator.Blur"/>.</summary>
        public static void Blur() => Active?.Blur();

        /// <summary>Notify that a screen closed (its per-screen nav state is dropped).</summary>
        public static void ScreenClosed(Screen screen) => Active?.ScreenClosed(screen);

        /// <summary>Move focus to a graph node by id.</summary>
        public static void FocusNode(Graph.ControlId id, bool announce = true) => Active?.FocusNode(id, announce);

        /// <summary>Move focus to the landing node of a Tab-stop.</summary>
        public static void FocusStop(object stopKey) => Active?.FocusStop(stopKey);

        /// <summary>The Tab-stop the focused node belongs to, or null.</summary>
        public static object FocusedStopKey => Active?.FocusedStopKey;
    }
}
