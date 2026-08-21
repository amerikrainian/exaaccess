using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;

namespace ExaAccess
{
    /// <summary>
    /// A reflection-cached view onto the live game, and the seam every accessibility read goes through —
    /// read the MODEL, never the pixels.
    ///
    /// The obfuscation reality (Eazfuscator + de4dot): the SHIPPING exe keeps real names for most TYPES
    /// (GameLogic, IScreen, DesktopScreen, Sim, …) but its MEMBERS are still `#=q…` gibberish. de4dot's
    /// readable member names (method_8, class5_0) exist ONLY in our local deobfuscated analysis copy, and
    /// de4dot does NOT preserve metadata tokens (it strips obfuscator junk, shifting them). What IT DOES
    /// preserve is the per-type ORDER of members — so de4dot's <c>method_N</c> is exactly the N-th method of
    /// that type in MetadataToken order, and the same ordinal selects the same member on the shipping exe.
    ///
    /// So we resolve TYPES by real name and MEMBERS by token-order ordinal (discovered from the deob copy),
    /// then cross-check each by signature so a future game update that shifts things fails loudly instead of
    /// silently patching the wrong method. Fields we can pin even harder — by their (unique) type — and only
    /// fall back to the ordinal if that fails.
    ///
    /// Ordinals (this game build — EXAPUNKS hasn't updated in years):
    ///   GameLogic method[8]  = init  (method_8: void, no params)     — one-time setup, window shown.
    ///   GameLogic method[25] = tick  (method_25: void, no params)    — the per-frame loop body.
    ///   GameLogic field[0]   = gameLogic_0 (static, typed GameLogic)  — live-instance self-reference.
    ///   GameLogic field[21]  = class5_0 (Class5&lt;IScreen&gt; stack) — the screen stack; top = last.
    /// </summary>
    public static class GameState
    {
        // Discovered de4dot ordinals (see class doc).
        private const int InitMethodOrdinal = 8;
        private const int TickMethodOrdinal = 25;
        private const int InstanceFieldOrdinal = 0;
        private const int ScreenStackFieldOrdinal = 21;

        private static Type _gameLogic;
        private static Type _iScreen;
        private static FieldInfo _instanceField;    // static GameLogic self-reference
        private static FieldInfo _screenStackField; // Class5<IScreen> stack
        private static FieldInfo _stackListField;   // Class5<T>.list_0 (List<IScreen>), resolved lazily

        /// <summary>The GameLogic init method (postfix target). Null if unresolved.</summary>
        public static MethodInfo InitMethod { get; private set; }

        /// <summary>The GameLogic per-frame tick method (prefix target). Null if unresolved.</summary>
        public static MethodInfo TickMethod { get; private set; }

        public static bool Bound => _gameLogic != null;

        /// <summary>Resolve the members we read/patch. Call once, right after the game assembly is loaded
        /// and before its entry point runs.</summary>
        public static void Bind(Assembly game)
        {
            _gameLogic = game.GetType("GameLogic");
            if (_gameLogic == null) { Log.Error("[gamestate] type 'GameLogic' not found — game layout changed?"); return; }
            _iScreen = game.GetType("IScreen");

            InitMethod = MethodByOrdinal(_gameLogic, InitMethodOrdinal, "init");
            TickMethod = MethodByOrdinal(_gameLogic, TickMethodOrdinal, "tick");
            _instanceField = ResolveInstanceField();
            _screenStackField = ResolveScreenStackField();

            Log.Info("[gamestate] bound: init=" + Describe(InitMethod) + " tick=" + Describe(TickMethod)
                + " instanceField=" + (_instanceField?.Name ?? "<null>") + " screenStackField=" + (_screenStackField?.Name ?? "<null>"));
        }

        /// <summary>The live <c>GameLogic</c> instance (boxed), or null before boot.</summary>
        public static object Instance => _instanceField?.GetValue(null);

        /// <summary>The active screen object (top of the stack), or null.</summary>
        public static object TopScreen()
        {
            var list = ScreenStack();
            if (list == null || list.Count == 0) return null;
            return list[list.Count - 1];
        }

        /// <summary>Concrete type name of the active screen (e.g. "DesktopScreen"), or null.</summary>
        public static string TopScreenName() => TopScreen()?.GetType().Name;

        /// <summary>The screen stack as a live IList (bottom → top), or null. Read-only use only.</summary>
        public static IList ScreenStack()
        {
            var gl = Instance;
            if (gl == null || _screenStackField == null) return null;
            var stack = _screenStackField.GetValue(gl);
            if (stack == null) return null;
            if (_stackListField == null)
            {
                // Class5<IScreen> has exactly one field: the List<IScreen> backing the stack. Match it by
                // type (its name is obfuscated), not by the de4dot name "list_0".
                foreach (var f in stack.GetType().GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
                {
                    if (f.FieldType.IsGenericType && f.FieldType.GetGenericTypeDefinition() == typeof(List<>))
                    { _stackListField = f; break; }
                }
            }
            return _stackListField?.GetValue(stack) as IList;
        }

        /// <summary>Type names of every screen on the stack, bottom → top. For dev inspection.</summary>
        public static List<string> ScreenStackNames()
        {
            var names = new List<string>();
            var list = ScreenStack();
            if (list != null)
                foreach (var s in list) names.Add(s?.GetType().Name ?? "<null>");
            return names;
        }

        // ---- resolution helpers (the ordinal primitive lives in MemberResolver) ----

        private static MethodInfo MethodByOrdinal(Type t, int ordinal, string role)
        {
            var m = MemberResolver.MethodByOrdinal(t, ordinal, role);
            // Cross-check: both init and tick are instance, void, no parameters. A mismatch means the
            // layout shifted — warn loudly so a game update can't silently patch a random method.
            if (m != null && (m.IsStatic || m.ReturnType != typeof(void) || m.GetParameters().Length != 0))
                Log.Warning("[gamestate] " + role + " method[" + ordinal + "] has unexpected signature (" + Describe(m) + ") — game may have updated.");
            return m;
        }

        private static FieldInfo ResolveInstanceField()
        {
            // Strong: the static field whose type IS GameLogic (the self-reference). Unique.
            foreach (var f in _gameLogic.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.DeclaredOnly))
                if (f.FieldType == _gameLogic) return f;
            Log.Warning("[gamestate] no static GameLogic self-field found; falling back to ordinal " + InstanceFieldOrdinal + ".");
            return FieldByOrdinal(InstanceFieldOrdinal);
        }

        private static FieldInfo ResolveScreenStackField()
        {
            // Strong: the instance field whose type is a generic-of-IScreen (Class5<IScreen>). The generic
            // arg IScreen keeps its real name even though Class5 itself is obfuscated.
            if (_iScreen != null)
            {
                foreach (var f in _gameLogic.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly))
                {
                    var ft = f.FieldType;
                    if (ft.IsGenericType)
                    {
                        var args = ft.GetGenericArguments();
                        if (args.Length == 1 && args[0] == _iScreen) return f;
                    }
                }
            }
            Log.Warning("[gamestate] no Class5<IScreen> field found by type; falling back to ordinal " + ScreenStackFieldOrdinal + ".");
            return FieldByOrdinal(ScreenStackFieldOrdinal);
        }

        private static FieldInfo FieldByOrdinal(int ordinal) => MemberResolver.FieldByOrdinal(_gameLogic, ordinal);

        private static string Describe(MethodInfo m) => MemberResolver.Describe(m);
    }
}
