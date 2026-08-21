using ExaAccess.Game;

namespace ExaAccess.Input
{
    /// <summary>
    /// The keyboard sensor for in-game input: a per-frame snapshot pair over SDL's internal key-state
    /// array (SDL_GetKeyboardState), giving edge queries (JustPressed/Released) plus Held, all by SDL
    /// scancode. Reading SDL's own state — not the game's derived key sets — keeps the mod's input
    /// independent of what the game consumes, needs no obfuscated members, and is exactly the sensor
    /// that already works during the splash. <see cref="Update"/> runs as the FIRST FrameLoop step so
    /// every consumer in the frame sees one coherent snapshot.
    /// </summary>
    public static class SdlKeyboard
    {
        private static byte[] _current;
        private static byte[] _previous;
        private static int _count;
        private static bool _primed; // first snapshot has no previous — suppress its edges

        public static void Update()
        {
            var swap = _previous;
            _previous = _current;
            _current = swap;

            int n = SdlNative.GetKeyboardState(ref _current);
            if (n == 0) { _primed = false; return; }
            if (_previous == null || _count != n)
            {
                // First frame (or SDL resized its table): duplicate so nothing edges spuriously.
                _previous = (byte[])_current.Clone();
                _count = n;
                _primed = true;
                return;
            }
            _primed = true;
        }

        public static bool Held(int scancode)
            => _primed && scancode >= 0 && scancode < _count && _current[scancode] != 0;

        public static bool JustPressed(int scancode)
            => _primed && scancode >= 0 && scancode < _count
               && _current[scancode] != 0 && _previous[scancode] == 0;

        public static bool Released(int scancode)
            => _primed && scancode >= 0 && scancode < _count
               && _current[scancode] == 0 && _previous[scancode] != 0;

        // SDL scancodes for the modifier keys.
        private const int LCtrl = 224, LShift = 225, LAlt = 226, RCtrl = 228, RShift = 229, RAlt = 230;

        public static bool CtrlHeld => Held(LCtrl) || Held(RCtrl);
        public static bool ShiftHeld => Held(LShift) || Held(RShift);
        public static bool AltHeld => Held(LAlt) || Held(RAlt);
    }

    /// <summary>The SDL scancodes bindings speak in (USB HID usage values — layout-independent
    /// physical keys). The common subset; extend as bindings need more.</summary>
    public enum Scancode
    {
        A = 4, B = 5, C = 6, D = 7, E = 8, F = 9, G = 10, H = 11, I = 12, J = 13, K = 14, L = 15,
        M = 16, N = 17, O = 18, P = 19, Q = 20, R = 21, S = 22, T = 23, U = 24, V = 25, W = 26,
        X = 27, Y = 28, Z = 29,
        Num1 = 30, Num2 = 31, Num3 = 32, Num4 = 33, Num5 = 34, Num6 = 35, Num7 = 36, Num8 = 37,
        Num9 = 38, Num0 = 39,
        Return = 40, Escape = 41, Backspace = 42, Tab = 43, Space = 44,
        Minus = 45, Equals = 46, LeftBracket = 47, RightBracket = 48, Backslash = 49,
        Semicolon = 51, Apostrophe = 52, Grave = 53, Comma = 54, Period = 55, Slash = 56,
        F1 = 58, F2 = 59, F3 = 60, F4 = 61, F5 = 62, F6 = 63, F7 = 64, F8 = 65, F9 = 66,
        F10 = 67, F11 = 68, F12 = 69,
        Insert = 73, Home = 74, PageUp = 75, Delete = 76, End = 77, PageDown = 78,
        Right = 79, Left = 80, Down = 81, Up = 82,
        KpEnter = 88,
    }
}
