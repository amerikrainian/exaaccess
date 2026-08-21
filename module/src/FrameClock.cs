using System.Diagnostics;

namespace ExaAccess
{
    /// <summary>Monotonic seconds since module load — the typematic/repeat clock (WrathAccess used
    /// Unity's Time.unscaledTime; there is no engine clock to borrow here). Wall-clock based, which is
    /// exactly right for input repeat: it must not stall when the game's sim hitches.</summary>
    internal static class FrameClock
    {
        private static readonly Stopwatch Watch = Stopwatch.StartNew();

        public static float Now => (float)Watch.Elapsed.TotalSeconds;
    }
}
