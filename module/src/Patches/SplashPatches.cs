using System;
using System.Reflection;
using ExaAccess.Game;
using ExaAccess.Localization;
using HarmonyLib;

namespace ExaAccess.Patches
{
    /// <summary>
    /// Makes ANY KEY advance the boot splash, which stock EXAPUNKS gates on a MOUSE CLICK only.
    /// Decompile facts (game/decompiled/GameLogic.cs, gate at the end of GameLogic.method_8): the
    /// gate waits on LOCALS but opens on any SDL type-1025 event, and its event loop DISCARDS key
    /// events — SDL's internal keyboard state is the only usable key sensor there. So:
    ///
    ///  • postfix LoadingScreenRenderer.method_2 (runs once when loading completes, right before the
    ///    gate) — arm + announce the localized prompt;
    ///  • postfix LoadingScreenRenderer.method_3 (the per-frame splash draw) — edge-poll the
    ///    keyboard against a baseline (keys held since launch don't count) and push a synthetic
    ///    1025 via SDL_PushEvent, which takes the game's own click path (music, sfx, fade);
    ///  • prefix GameLogic.method_24 (the first call after the gate) — disarm for the session.
    ///
    /// Targets are TYPED (Expr.MethodOf so the references remap); method_24 is private, so it goes
    /// through the Deobf name lookup.
    /// </summary>
    internal static class SplashPatches
    {
        private static bool _armed;
        private static bool _pushed;
        private static byte[] _baseline;
        private static byte[] _current;
        private static int _keyCount;

        public static void Apply(Harmony harmony)
        {
            var ready = Expr.MethodOf(() => default(LoadingScreenRenderer).method_2());
            var draw = Expr.MethodOf(() => default(LoadingScreenRenderer).method_3(0f, false));
            var afterGate = Deobf.Method(typeof(GameLogic), "method_24"); // private void ()
            if (afterGate == null) { Log.Error("[splash] post-splash method unresolved — skipping."); return; }

            var self = typeof(SplashPatches);
            harmony.Patch(ready, postfix: new HarmonyMethod(self.GetMethod(nameof(AfterSplashReady), BindingFlags.NonPublic | BindingFlags.Static)));
            harmony.Patch(draw, postfix: new HarmonyMethod(self.GetMethod(nameof(AfterSplashDraw), BindingFlags.NonPublic | BindingFlags.Static)));
            harmony.Patch(afterGate, prefix: new HarmonyMethod(self.GetMethod(nameof(BeforePostSplash), BindingFlags.NonPublic | BindingFlags.Static)));
            Log.Info("[splash] any-key advance attached.");
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
