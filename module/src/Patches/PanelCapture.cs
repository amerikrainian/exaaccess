using System;
using System.Collections.Generic;
using System.Reflection;
using ExaAccess.Game;
using ExaAccess.UI;
using HarmonyLib;

namespace ExaAccess.Patches
{
    /// <summary>
    /// Reads the special-puzzle panels (I/O logs, uplink status, custom-puzzle windows) the way the
    /// game writes them: every panel is real text drawn from inside the puzzle logic's four draw
    /// hooks (GClass298.vmethod_0..3 overrides), so a depth flag around those plus a postfix on the
    /// GClass230 text statics captures each frame's panel text — any puzzle, no per-puzzle code.
    /// PanelText reassembles reading order; the editor screen publishes once per tick and shows the
    /// lines in its goal popup. ForceGoal makes EditorScreen.method_7() report true so the game
    /// itself renders its F1 goal view (full expected streams, ghost files) — draw-only by
    /// construction: the sim's own register reads hardcode the flag false (GClass218).
    /// Capture is armed only while the popup wants it; disarmed, the hot text path costs one
    /// static bool check.
    /// </summary>
    internal static class PanelCapture
    {
        private static bool _armed;
        private static bool _record;
        private static int _depth;
        private static readonly List<PanelCell> Buffer = new List<PanelCell>();
        private const int BufferCap = 1024;

        /// <summary>While true, EditorScreen.method_7() (the F1 show-goal flag) reads true.</summary>
        public static bool ForceGoal { get; private set; }

        /// <summary>Last published frame's panel text, in reading order.</summary>
        public static List<string> Lines { get; private set; } = new List<string>();

        public static void SetArmed(bool armed, bool forceGoal)
        {
            _armed = armed;
            ForceGoal = forceGoal;
            if (!armed && (Buffer.Count > 0 || Lines.Count > 0))
            {
                Buffer.Clear();
                Lines = new List<string>();
            }
        }

        /// <summary>Once per tick (the editor's OnUpdate): the buffer holds the previous frame's
        /// complete draw — our tick runs from the game-tick prefix, before this frame draws.</summary>
        public static void Publish()
        {
            if (Buffer.Count == 0 && Lines.Count == 0) return;
            Lines = PanelText.Assemble(Buffer);
            Buffer.Clear();
        }

        public static void Apply(Harmony harmony)
        {
            try
            {
                harmony.Patch(Expr.MethodOf(() => default(EditorScreen).method_7()),
                    prefix: new HarmonyMethod(typeof(PanelCapture), nameof(ShowGoalPrefix)));

                // Every override of the four draw hooks, found by slot (the types are enumerated at
                // runtime — their obfuscated names never appear here).
                var slots = new[]
                {
                    Expr.MethodOf(() => default(GClass298).vmethod_0(0f, 0f, false)),
                    Expr.MethodOf(() => default(GClass298).vmethod_1(0f, false)),
                    Expr.MethodOf(() => default(GClass298).vmethod_2(0f, false)),
                    Expr.MethodOf(() => default(GClass298).vmethod_3(0f, false)),
                };
                var prefix = new HarmonyMethod(typeof(PanelCapture), nameof(DrawPrefix));
                var finalizer = new HarmonyMethod(typeof(PanelCapture), nameof(DrawFinalizer));
                Type[] types;
                try { types = typeof(GClass298).Assembly.GetTypes(); }
                catch (ReflectionTypeLoadException ex) { types = ex.Types; }
                int patched = 0;
                foreach (var type in types)
                {
                    if (type == null || !type.IsSubclassOf(typeof(GClass298))) continue;
                    foreach (var m in type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly))
                    {
                        var baseDef = m.GetBaseDefinition();
                        if (baseDef == m || baseDef.DeclaringType != typeof(GClass298)) continue;
                        foreach (var slot in slots)
                        {
                            if (baseDef.MetadataToken != slot.MetadataToken) continue;
                            harmony.Patch(m, prefix: prefix, finalizer: finalizer);
                            patched++;
                        }
                    }
                }

                var text = new HarmonyMethod(typeof(PanelCapture), nameof(TextPostfix));
                harmony.Patch(Expr.MethodOf(() => GClass230.smethod_33(null, default(Vector2), null,
                        default(Color), default(GEnum165), 0f, 0f, 0f, 0f, 0, default(Color), 0,
                        (Matrix4?)null, 0f, default(Color), false, false)), postfix: text);
                harmony.Patch(Expr.MethodOf(() => GClass230.smethod_34(null, default(Vector2), null,
                        default(Color), default(GEnum165), 0f, 0f, 0f, 0f, 0, default(Color), 0,
                        (Matrix4?)null, 0f, default(Color), false, false)), postfix: text);
                harmony.Patch(Expr.MethodOf(() => GClass230.smethod_36(null, default(Vector2), null,
                        default(Color), default(GEnum165), 0f, 0f, 0f, 0f, 0, default(Color), 0)),
                    postfix: text);
                Log.Info("[patch] panel capture armed (" + patched + " draw hooks)");
            }
            catch (Exception ex) { Log.Error("[patch] panel capture failed to apply", ex); }
        }

        private static bool ShowGoalPrefix(ref bool __result)
        {
            if (!ForceGoal) return true;
            __result = true;
            return false;
        }

        private static void DrawPrefix()
        {
            _depth++;
            _record = _armed;
        }

        // A finalizer, not a postfix — the depth must unwind even when a draw hook throws.
        private static Exception DrawFinalizer(Exception __exception)
        {
            if (--_depth <= 0)
            {
                _depth = 0;
                _record = false;
            }
            return __exception;
        }

        private static void TextPostfix(string __0, Vector2 __1)
        {
            if (!_record) return;
            try
            {
                if (!string.IsNullOrWhiteSpace(__0) && Buffer.Count < BufferCap)
                    Buffer.Add(new PanelCell(__0, __1.float_0, __1.float_1));
            }
            catch { }
        }
    }
}
