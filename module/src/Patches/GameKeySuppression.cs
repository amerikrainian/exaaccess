using System;
using System.Collections.Generic;
using ExaAccess.Game;
using HarmonyLib;
using SDL2;

namespace ExaAccess.Patches
{
    /// <summary>
    /// The focus-mode key-suppression seam. Some game screens read the keyboard themselves through
    /// the game's input facade (deob GClass64) — the desktop's arrows/Enter/Home/End move its task
    /// selection. While focus mode is ON and a MODELED screen is focused, our navigator owns those
    /// keys, so the game's readers must see them as not-pressed or every press double-acts (an
    /// arrow would move both our focus and the game's selection).
    ///
    /// Two prefixes cover the whole navigation vocabulary: smethod_17 (just-pressed by keycode —
    /// Enter/KP-Enter, Home/End, and every composed helper such as smethod_15/16 ride it) and
    /// smethod_22 (pressed-with-repeat — the arrow keys incl. their numpad variants, PageUp/Down).
    /// Both are PURE reads of the per-frame snapshot sets (decompile-verified: no consumption, no
    /// side effects), so skipping the original is safe. Only the keys in <see cref="Keys"/> are
    /// swallowed — Escape and everything else pass through untouched (Escape closing the desktop
    /// is native behavior we keep). Suppression never applies while the focused screen
    /// CapturesRawInput (the key-capture overlay must see raw keys), on unmodeled screens (the
    /// game must stay fully playable), or with focus mode off.
    /// </summary>
    internal static class GameKeySuppression
    {
        // SDL KEYCODES the navigator's vocabulary claims (ui.* bindings + their numpad aliases).
        private static readonly HashSet<int> Keys = new HashSet<int>
        {
            13,          // Return        (ui.activate)
            9,           // Tab           (ui.next/prev)
            8,           // Backspace     (ui.secondary)
            32,          // Space         (ui.tooltip)
            1073741912,  // KP_Enter      (ui.activate)
            1073741903,  // Right
            1073741904,  // Left
            1073741905,  // Down
            1073741906,  // Up
            1073741914,  // KP_2 (down)
            1073741916,  // KP_4 (left)
            1073741918,  // KP_6 (right)
            1073741920,  // KP_8 (up)
            1073741898,  // Home          (ui.home)
            1073741899,  // PageUp        (game task-list jump; suppressed with the rest)
            1073741901,  // End           (ui.end)
            1073741902,  // PageDown
        };

        public static void Apply(Harmony harmony)
        {
            try
            {
                var prefix = new HarmonyMethod(typeof(GameKeySuppression), nameof(KeyPrefix));
                harmony.Patch(Expr.MethodOf(() => GClass64.smethod_17(default(SDL.GEnum195))), prefix: prefix);
                harmony.Patch(Expr.MethodOf(() => GClass64.smethod_22(default(SDL.GEnum195))), prefix: prefix);
                Log.Info("[patch] game key suppression armed");
            }
            catch (Exception ex) { Log.Error("[patch] key suppression failed to apply", ex); }
        }

        private static bool Suppressing()
        {
            try
            {
                if (!FocusMode.Active) return false;
                var cur = Screens.ScreenManager.Current;
                return cur != null && !cur.CapturesRawInput;
            }
            catch { return false; }
        }

        // __0 = the keycode argument (positional injection — the shipping method's parameter
        // names are obfuscated, so never bind by name here).
        private static bool KeyPrefix(SDL.GEnum195 __0, ref bool __result)
        {
            try
            {
                // A focused text field owns Backspace (the append widget's one keycode poll);
                // printable characters ride the TEXTINPUT channel, which is never suppressed.
                if ((int)__0 == 8 && UI.Navigation.TextEntryFocused) return true;
                if (Keys.Contains((int)__0) && Suppressing())
                {
                    __result = false;
                    return false;
                }
            }
            catch { }
            return true;
        }
    }
}
