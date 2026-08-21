using System;

namespace ExaAccess.Input
{
    /// <summary>
    /// A keyboard chord over <see cref="SdlKeyboard"/> — WrathAccess's KeyboardBinding rebuilt on SDL
    /// scancodes (there is no Unity Input here). Modifiers are EXACT-match, not "at least": a bare-A
    /// binding must not also fire under Ctrl+A.
    /// </summary>
    public sealed class SdlKeyboardBinding : InputBinding
    {
        public Scancode Key { get; }
        public bool Ctrl { get; }
        public bool Shift { get; }
        public bool Alt { get; }

        public SdlKeyboardBinding(Scancode key, bool ctrl = false, bool shift = false, bool alt = false)
        {
            Key = key;
            Ctrl = ctrl;
            Shift = shift;
            Alt = alt;
        }

        private bool ModifiersMatch()
            => Ctrl == SdlKeyboard.CtrlHeld && Shift == SdlKeyboard.ShiftHeld && Alt == SdlKeyboard.AltHeld;

        public override bool JustPressed() => ModifiersMatch() && SdlKeyboard.JustPressed((int)Key);
        public override bool Held() => ModifiersMatch() && SdlKeyboard.Held((int)Key);
        public override bool Released() => ModifiersMatch() && SdlKeyboard.Released((int)Key);

        public override string DisplayName
        {
            get
            {
                var sb = new System.Text.StringBuilder();
                if (Ctrl) sb.Append("Ctrl+");
                if (Shift) sb.Append("Shift+");
                if (Alt) sb.Append("Alt+");
                sb.Append(Key);
                return sb.ToString();
            }
        }

        public override string Type => "keyboard";

        /// <summary>"A|ctrl,shift" — the WrathAccess wire shape, scancode names instead of KeyCodes.</summary>
        public override string Serialize()
        {
            var mods = new System.Collections.Generic.List<string>();
            if (Ctrl) mods.Add("ctrl");
            if (Shift) mods.Add("shift");
            if (Alt) mods.Add("alt");
            return Key + "|" + string.Join(",", mods);
        }

        public static SdlKeyboardBinding Deserialize(string data)
        {
            try
            {
                var parts = (data ?? "").Split('|');
                Scancode key;
                if (!Enum.TryParse(parts[0], out key)) return null;
                bool ctrl = false, shift = false, alt = false;
                if (parts.Length > 1)
                    foreach (var m in parts[1].Split(','))
                    {
                        if (m == "ctrl") ctrl = true;
                        else if (m == "shift") shift = true;
                        else if (m == "alt") alt = true;
                    }
                return new SdlKeyboardBinding(key, ctrl, shift, alt);
            }
            catch { return null; }
        }
    }
}
