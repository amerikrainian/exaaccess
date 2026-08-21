using System;

namespace ExaAccess.Game
{
    /// <summary>
    /// Game operations the mod invokes — TYPED against the deob reference assembly (the load-time
    /// remap binds these to the shipping names; see Modularity/GameRefRemapper). What used to be a
    /// resolver stack is now the game's own calls, wrapped only for null-safety and logging:
    /// method_12 = push screen, method_15 = pop, method_32 = quit (decompile-verified — the same
    /// calls the game's own click handlers make).
    /// </summary>
    internal static class GameApi
    {
        public static bool PushScreen(IScreen screen)
        {
            try
            {
                var gl = GameLogic.gameLogic_0;
                if (gl == null || screen == null) return false;
                gl.method_12(screen);
                Log.Info("[game] pushed screen " + screen.GetType().Name);
                return true;
            }
            catch (Exception ex) { Log.Error("[game] PushScreen failed", ex); return false; }
        }

        public static bool PopScreen()
        {
            try
            {
                var gl = GameLogic.gameLogic_0;
                if (gl == null) return false;
                gl.method_15();
                return true;
            }
            catch (Exception ex) { Log.Error("[game] PopScreen failed", ex); return false; }
        }

        public static bool QuitGame()
        {
            try
            {
                var gl = GameLogic.gameLogic_0;
                if (gl == null) return false;
                gl.method_32(0);
                return true;
            }
            catch (Exception ex) { Log.Error("[game] QuitGame failed", ex); return false; }
        }
    }
}
