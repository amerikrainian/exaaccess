namespace ExaAccess.Localization
{
    /// <summary>Call-site shorthand for the "ui" table (the WrathAccess idiom): every string the mod
    /// speaks goes through here or Message — hardcoded speakable English is a hard-rule violation.</summary>
    public static class Loc
    {
        public static string T(string key) => Message.Localized("ui", key).Resolve();
        public static string T(string key, object args) => Message.Localized("ui", key, args).Resolve();
    }
}
