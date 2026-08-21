using System;
using System.Reflection;

namespace ExaAccess.Game
{
    /// <summary>
    /// Resolved game operations the mod invokes — the write-side counterpart of GameState's reads.
    /// Everything resolves lazily with signature cross-checks and fails loudly to a no-op (hard rule:
    /// never act through a maybe-wrong member). Decompile facts (game/decompiled/GameLogic.cs):
    /// `GameLogic.method_12(IScreen)` pushes a screen — it is exactly what the title screen's own
    /// click handlers call, so activating via it is byte-identical to a mouse click.
    /// </summary>
    internal static class GameApi
    {
        private static MethodInfo _pushScreen;
        private static bool _pushResolved;

        /// <summary>Instantiate <paramref name="screenType"/> and push it via the game's own
        /// screen-push method. Main thread only. False (logged) when anything fails.</summary>
        public static bool PushScreen(Type screenType, params object[] ctorArgs)
        {
            try
            {
                var gameLogic = GameState.Instance;
                if (gameLogic == null || screenType == null) return false;
                var push = ResolvePush(gameLogic.GetType());
                if (push == null) return false;
                object screen = Activator.CreateInstance(screenType, ctorArgs);
                push.Invoke(gameLogic, new[] { screen });
                Log.Info("[game] pushed screen " + screenType.Name);
                return true;
            }
            catch (Exception ex)
            {
                Log.Error("[game] PushScreen(" + (screenType != null ? screenType.Name : "<null>") + ") failed", ex);
                return false;
            }
        }

        private static MethodInfo ResolvePush(Type gameLogic)
        {
            if (_pushResolved) return _pushScreen;
            _pushResolved = true;

            var iScreen = GameState.GameAssembly.GetType("IScreen");
            if (iScreen == null) { Log.Error("[game] IScreen not found — cannot resolve the screen push."); return null; }

            // de4dot ordinal 12 (method_12), cross-checked: instance void(IScreen).
            var byOrdinal = MemberResolver.MethodByOrdinal(gameLogic, 12, "push-screen");
            if (IsPushSignature(byOrdinal, iScreen))
            {
                _pushScreen = byOrdinal;
            }
            else
            {
                // Layout shifted: accept the method only if the signature is UNIQUE on the type.
                MethodInfo unique = null;
                foreach (var m in MemberResolver.MethodsInTokenOrder(gameLogic))
                {
                    if (!IsPushSignature(m, iScreen)) continue;
                    if (unique != null) { unique = null; break; }
                    unique = m;
                }
                _pushScreen = unique;
                if (unique == null)
                    Log.Error("[game] push-screen unresolved (ordinal 12 mismatched and void(IScreen) is not unique) — game updated?");
            }

            if (_pushScreen != null)
                Log.Info("[game] push-screen = " + MemberResolver.Describe(_pushScreen));
            return _pushScreen;
        }

        private static bool IsPushSignature(MethodInfo m, Type iScreen)
        {
            if (m == null || m.IsStatic || m.ReturnType != typeof(void)) return false;
            var ps = m.GetParameters();
            return ps.Length == 1 && ps[0].ParameterType == iScreen;
        }

        // ---- pop / quit (resolved on first use, like the push) ----

        private static MethodInfo _popScreen;
        private static bool _popResolved;

        /// <summary>Pop the game's top screen (GameLogic.method_15) — what the game's own Escape/X
        /// handlers call. Main thread only.</summary>
        public static bool PopScreen()
        {
            var gl = GameState.Instance;
            if (gl == null) return false;
            if (!_popResolved)
            {
                _popResolved = true;
                _popScreen = ResolveInstanceMethod(gl.GetType(), 15, "pop-screen", typeof(void));
            }
            if (_popScreen == null) return false;
            try { _popScreen.Invoke(gl, null); return true; }
            catch (Exception ex) { Log.Error("[game] PopScreen failed", ex); return false; }
        }

        private static MethodInfo _quit;
        private static bool _quitResolved;

        /// <summary>Quit EXAPUNKS (GameLogic.method_32(0)) — the control panel's Exit Game button.</summary>
        public static bool QuitGame()
        {
            var gl = GameState.Instance;
            if (gl == null) return false;
            if (!_quitResolved)
            {
                _quitResolved = true;
                _quit = ResolveInstanceMethod(gl.GetType(), 32, "quit", typeof(void), typeof(int));
            }
            if (_quit == null) return false;
            try { _quit.Invoke(gl, new object[] { 0 }); return true; }
            catch (Exception ex) { Log.Error("[game] QuitGame failed", ex); return false; }
        }

        /// <summary>Resolve de4dot's method_&lt;index&gt; on the shipping exe: the index counts INSTANCE
        /// methods only, in token order (statics get their own smethod_ counter). The ordinal pick must
        /// match the expected signature; otherwise the unique-signature scan decides; otherwise null,
        /// loudly — never a maybe-wrong member.</summary>
        internal static MethodInfo ResolveInstanceMethod(Type type, int deobIndex, string role, Type returnType, params Type[] paramTypes)
        {
            var instance = new System.Collections.Generic.List<MethodInfo>();
            foreach (var m in MemberResolver.MethodsInTokenOrder(type))
                if (!m.IsStatic) instance.Add(m);

            var byIndex = (deobIndex >= 0 && deobIndex < instance.Count) ? instance[deobIndex] : null;
            if (Matches(byIndex, returnType, paramTypes))
            {
                Log.Info("[resolve] " + role + " = " + MemberResolver.Describe(byIndex));
                return byIndex;
            }

            MethodInfo unique = null;
            foreach (var m in instance)
            {
                if (!Matches(m, returnType, paramTypes)) continue;
                if (unique != null) { unique = null; break; }
                unique = m;
            }
            if (unique != null)
                Log.Warning("[resolve] " + role + " ordinal " + deobIndex + " mismatched; unique signature match "
                    + MemberResolver.Describe(unique) + " — game may have updated.");
            else
                Log.Error("[resolve] " + role + " unresolved on " + type.Name + " (ordinal mismatch, signature not unique).");
            return unique;
        }

        private static bool Matches(MethodInfo m, Type returnType, Type[] paramTypes)
        {
            if (m == null || m.ReturnType != returnType) return false;
            var ps = m.GetParameters();
            if (ps.Length != paramTypes.Length) return false;
            for (int i = 0; i < ps.Length; i++)
                if (ps[i].ParameterType != paramTypes[i]) return false;
            return true;
        }
    }
}
