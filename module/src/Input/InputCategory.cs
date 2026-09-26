namespace Echopunks.Input
{
    /// <summary>
    /// The input layer an action belongs to (ported from WrathAccess, trimmed to what exists here —
    /// game-area categories get added as screens land). Screens will declare which categories they use,
    /// in priority order; an identical chord bound in two live categories resolves to the
    /// higher-priority one (shadowing). <see cref="Global"/> is always live.
    /// </summary>
    public enum InputCategory
    {
        /// <summary>Always live, even when focus mode is off (focus toggle, mod hotkeys).</summary>
        Global,
        /// <summary>Screen/menu navigation — live when the focused screen declares it.</summary>
        UI,
    }
}
