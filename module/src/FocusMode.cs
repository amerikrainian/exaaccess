namespace Echopunks
{
    /// <summary>
    /// Whether the mod's navigation owns the keyboard. WrathAccess pairs this with the game's own
    /// "keyboard disabled" lever; EXAPUNKS has no such lever, so for now this is a plain flag that
    /// gates nav dispatch and announcements — ON by default (the game barely uses the keyboard
    /// outside the EXA code editor). The suppression story (swallowing our nav keys so busy screens
    /// don't also react) lands with the screens that need it — see the CLAUDE.md roadmap.
    /// </summary>
    public static class FocusMode
    {
        public static bool Active = true;
    }
}
