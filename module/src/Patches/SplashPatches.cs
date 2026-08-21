using System;
using System.Reflection;
using ExaAccess.Game;
using ExaAccess.Localization;
using HarmonyLib;

namespace ExaAccess.Patches
{
    /// <summary>
    /// Makes ANY KEY advance the boot splash, which stock EXAPUNKS gates on a MOUSE CLICK only — an
    /// unprompted, invisible wall for a blind player. Decompile findings (game/decompiled/GameLogic.cs,
    /// splash gate at the end of GameLogic.method_8):
    ///
    ///  • The gate waits on LOCALS, so no field can be poked; but the ONLY thing that opens it is an
    ///    SDL event with type 1025 (SDL_MOUSEBUTTONDOWN) — button/coords ignored, repeat-safe.
    ///  • The gate's event loop DISCARDS key events without populating the game's own key sets, so
    ///    SDL's internal keyboard state array (SDL_GetKeyboardState) is the only usable key sensor there.
    ///  • LoadingScreenRenderer.method_2() (ordinal 2) runs once when loading completes, immediately
    ///    before the gate loop — our "splash is waiting" signal, where the prompt is announced.
    ///  • LoadingScreenRenderer.method_3(float, bool) draws the splash once per gate iteration — the
    ///    per-frame, main-thread hook to poll keys from. Resolved BY SIGNATURE: it is the only
    ///    void(float, bool) on the type. (It also runs during the loading-progress phase, but the
    ///    _armed flag from method_2 scopes us to the gate.)
    ///  • GameLogic.method_24() (ordinal 24) is the first call after the gate — disarm, so a synthetic
    ///    click can never fire during gameplay.
    ///
    /// On a new key press (edge-detected against a baseline, so a key held since launch doesn't
    /// auto-skip), a synthetic 1025 goes through SDL_PushEvent and the game takes its own click path —
    /// menu music, click sound, the 0.5 s fade — identical to a real mouse click.
    /// </summary>
    internal static class SplashPatches
    {
        private static bool _armed;
        private static bool _pushed;
        private static byte[] _baseline;
        private static byte[] _current;
        private static int _keyCount;

        /// <summary>Resolve the three targets and patch. No-op (with a loud log) if any target fails its
        /// cross-check — never patch a maybe-wrong method. Harmony instance is the module's per-load one.</summary>
        public static void Apply(Harmony harmony)
        {
            var game = GameState.GameAssembly;
            if (game == null) { Log.Error("[splash] no game assembly bound — skipping."); return; }

            var renderer = game.GetType("LoadingScreenRenderer");
            var gameLogic = game.GetType("GameLogic");
            if (renderer == null || gameLogic == null)
            {
                Log.Error("[splash] LoadingScreenRenderer/GameLogic not found — game layout changed? Skipping.");
                return;
            }

            // Splash-ready: ordinal 2, cross-checked public void() instance.
            var ready = MemberResolver.MethodByOrdinal(renderer, 2, "splash-ready");
            if (ready == null || ready.IsStatic || !ready.IsPublic
                || ready.ReturnType != typeof(void) || ready.GetParameters().Length != 0)
            {
                Log.Error("[splash] splash-ready method failed its signature cross-check (" + MemberResolver.Describe(ready) + ") — skipping.");
                return;
            }

            // Per-frame splash draw: the unique void(float, bool) instance method.
            MethodInfo draw = null;
            foreach (var m in MemberResolver.MethodsInTokenOrder(renderer))
            {
                if (m.IsStatic || m.ReturnType != typeof(void)) continue;
                var ps = m.GetParameters();
                if (ps.Length != 2 || ps[0].ParameterType != typeof(float) || ps[1].ParameterType != typeof(bool)) continue;
                if (draw != null) { draw = null; break; } // not unique anymore — game updated; bail
                draw = m;
            }
            if (draw == null)
            {
                Log.Error("[splash] no unique void(float,bool) splash-draw method — game updated? Skipping.");
                return;
            }

            // Post-splash: ordinal 24, cross-checked void() instance.
            var after = MemberResolver.MethodByOrdinal(gameLogic, 24, "post-splash");
            if (after == null || after.IsStatic || after.ReturnType != typeof(void) || after.GetParameters().Length != 0)
            {
                Log.Error("[splash] post-splash method failed its signature cross-check (" + MemberResolver.Describe(after) + ") — skipping.");
                return;
            }

            var self = typeof(SplashPatches);
            harmony.Patch(ready, postfix: new HarmonyMethod(self.GetMethod(nameof(AfterSplashReady), BindingFlags.NonPublic | BindingFlags.Static)));
            harmony.Patch(draw, postfix: new HarmonyMethod(self.GetMethod(nameof(AfterSplashDraw), BindingFlags.NonPublic | BindingFlags.Static)));
            harmony.Patch(after, prefix: new HarmonyMethod(self.GetMethod(nameof(BeforePostSplash), BindingFlags.NonPublic | BindingFlags.Static)));
            Log.Info("[splash] any-key advance attached (ready=" + MemberResolver.Describe(ready)
                + ", draw=" + MemberResolver.Describe(draw) + ", after=" + MemberResolver.Describe(after) + ").");
        }

        private static void AfterSplashReady()
        {
            try
            {
                if (_armed) return;
                _armed = true;
                _baseline = null; // captured on the first draw poll, so keys held since launch don't count
                Log.Info("[splash] gate open — announcing the prompt.");
                Speech.Tts.Speak(Loc.T("splash.press_any_key"));
            }
            catch (Exception ex) { Log.Error("[splash] ready hook failed", ex); }
        }

        private static void AfterSplashDraw()
        {
            try
            {
                if (!_armed || _pushed) return;

                int n = SdlNative.GetKeyboardState(ref _current);
                if (n == 0) return;
                if (_baseline == null || _keyCount != n)
                {
                    _baseline = (byte[])_current.Clone();
                    _keyCount = n;
                    return;
                }

                for (int i = 0; i < n; i++)
                {
                    if (_current[i] != 0 && _baseline[i] == 0)
                    {
                        _pushed = true; // the gate is idempotent, but once is enough
                        Log.Info("[splash] key (scancode " + i + ") — pushing the synthetic click.");
                        SdlNative.PushMouseButtonDown();
                        return;
                    }
                    _baseline[i] = _current[i]; // released keys re-arm
                }
            }
            catch (Exception ex)
            {
                Log.Error("[splash] draw poll failed — disabling", ex);
                _armed = false;
            }
        }

        private static void BeforePostSplash()
        {
            // First statement after the gate loop: stand down for good this session.
            _armed = false;
            _pushed = false;
            _baseline = null;
        }
    }
}
