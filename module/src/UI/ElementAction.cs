using System;

namespace Echopunks.UI
{
    /// <summary>A screen-level action (Back/Escape handlers and the like), dispatched by id — the
    /// WrathAccess shape, trimmed to the one id in use. Screens advertise them via GetActions.</summary>
    public sealed class ElementAction
    {
        public string Id { get; }
        public Action Handler { get; }

        public ElementAction(string id, Action handler)
        {
            Id = id;
            Handler = handler;
        }

        public void Execute() => Handler?.Invoke();
    }

    public static class ActionIds
    {
        public const string Back = "back";
    }
}
