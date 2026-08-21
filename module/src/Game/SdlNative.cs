using System;
using System.Runtime.InteropServices;

namespace ExaAccess.Game
{
    /// <summary>
    /// The mod's own P/Invoke over the game's SDL2.dll (already loaded in-process, so the module name
    /// resolves to the loaded copy). Two capabilities: reading SDL's internal keyboard state — the only
    /// input sensor that works during the boot splash, whose gate loop discards key events without
    /// populating the game's own key sets — and pushing synthetic events into the queue the game reads.
    /// </summary>
    internal static class SdlNative
    {
        private const string Dll = "SDL2.dll";

        /// <summary>SDL event type: SDL_MOUSEBUTTONDOWN — the one event the splash gate accepts.</summary>
        public const uint MouseButtonDownEvent = 1025;

        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl, EntryPoint = "SDL_GetKeyboardState")]
        private static extern IntPtr GetKeyboardStateRaw(out int numkeys);

        /// <summary>Copy SDL's internal held-key array (1 byte per scancode) into
        /// <paramref name="buffer"/> (grown as needed); returns the number of valid entries. SDL updates
        /// the array during its event pump regardless of whether the app consumes the key events.</summary>
        public static int GetKeyboardState(ref byte[] buffer)
        {
            int numkeys;
            IntPtr state = GetKeyboardStateRaw(out numkeys);
            if (state == IntPtr.Zero || numkeys <= 0) return 0;
            if (buffer == null || buffer.Length < numkeys) buffer = new byte[numkeys];
            Marshal.Copy(state, buffer, 0, numkeys);
            return numkeys;
        }

        /// <summary>True while the scancode is held, straight off SDL's state array (one-off query).</summary>
        public static bool ScancodeHeld(int scancode)
        {
            int numkeys;
            IntPtr state = GetKeyboardStateRaw(out numkeys);
            if (state == IntPtr.Zero || scancode < 0 || scancode >= numkeys) return false;
            return Marshal.ReadByte(state, scancode) != 0;
        }

        // SDL_Event is a 56-byte union; SDL copies the full union from the pointer we hand it, so the
        // managed struct must be at least that big. First fields mirror SDL_MouseButtonEvent.
        [StructLayout(LayoutKind.Sequential, Size = 64)]
        public struct Event
        {
            public uint Type;
            public uint Timestamp;
            public uint WindowId;
            public uint Which;
            public byte Button;
            public byte State;
            public byte Clicks;
            public byte Padding;
            public int X;
            public int Y;
        }

        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl, EntryPoint = "SDL_PushEvent")]
        private static extern int PushEventRaw(ref Event ev);

        /// <summary>Deposit a synthetic left-mouse-down into SDL's event queue — exactly what the splash
        /// gate waits for (it reads only the type; SDL_PushEvent has none of the focus/injection filters
        /// a posted Windows message would run through). True when the event was queued.</summary>
        public static bool PushMouseButtonDown()
        {
            var ev = new Event { Type = MouseButtonDownEvent, Button = 1, State = 1 };
            return PushEventRaw(ref ev) >= 1;
        }
    }
}
