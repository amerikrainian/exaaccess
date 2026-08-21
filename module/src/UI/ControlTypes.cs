using ExaAccess.Localization;
using ExaAccess.UI.Graph;

namespace ExaAccess.UI
{
    /// <summary>
    /// The control-type registry (ported from WrathAccess): each entry is a <see cref="ControlType"/>
    /// VALUE — its settings key, the speak order of its announcement kinds, and the parts common to
    /// every control of the type (the localized role word). A node factory sets the type and gets the
    /// role word and the ordering for free. Only the game-agnostic types are ported; EXAPUNKS-specific
    /// ones (an EXA register, a file cell) get added here as screens need them.
    /// </summary>
    public static class ControlTypes
    {
        private static readonly string[] StandardOrder =
        {
            AnnouncementKinds.Label,
            AnnouncementKinds.Role,
            AnnouncementKinds.Value,
            AnnouncementKinds.Selected,
            AnnouncementKinds.Enabled,
            AnnouncementKinds.Tooltip,
            AnnouncementKinds.Position,
        };

        private static NodeAnnouncement[] RoleWord(string word)
            => new[] { new NodeAnnouncement(() => Loc.T("role." + word), kind: AnnouncementKinds.Role) };

        public static readonly ControlType Button = new ControlType
        {
            Key = "button",
            Order = StandardOrder,
            Common = () => RoleWord("button"),
        };

        public static readonly ControlType Toggle = new ControlType
        {
            Key = "toggle",
            Order = StandardOrder,
            Common = () => RoleWord("toggle"),
        };

        public static readonly ControlType Slider = new ControlType
        {
            Key = "slider",
            Order = StandardOrder,
            Common = () => RoleWord("slider"),
        };

        /// <summary>One option of a single-select group (dropdown options, tab rows).</summary>
        public static readonly ControlType RadioButton = new ControlType
        {
            Key = "radio_button",
            Order = StandardOrder,
            Common = () => RoleWord("radio button"),
        };

        /// <summary>A dropdown: value = the current option; activation opens the option list.</summary>
        public static readonly ControlType ComboBox = new ControlType
        {
            Key = "combo_box",
            Order = StandardOrder,
            Common = () => RoleWord("combo box"),
        };

        /// <summary>A tab in a tab strip.</summary>
        public static readonly ControlType Tab = new ControlType
        {
            Key = "tab",
            Order = StandardOrder,
            Common = () => RoleWord("tab"),
        };

        /// <summary>An expandable group header (a tree section). No role word of its own — the
        /// announcer appends the expanded/collapsed state word.</summary>
        public static readonly ControlType Group = new ControlType
        {
            Key = "group",
            Order = StandardOrder,
        };

        /// <summary>A read-only text line — no role word; typed so its parts stay configurable.</summary>
        public static readonly ControlType Text = new ControlType
        {
            Key = "text",
            Order = StandardOrder,
        };

        /// <summary>Every registered type (future per-type announcement settings key off this).</summary>
        public static readonly ControlType[] All = { Button, Toggle, Slider, RadioButton, ComboBox, Tab, Group, Text };
    }
}
