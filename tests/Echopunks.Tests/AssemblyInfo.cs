// Localization/speech state is static (by design — it mirrors the mod's runtime shape), so tests
// that touch it must not interleave. The suite is small; sequential is simplest and deterministic.
[assembly: Xunit.CollectionBehavior(DisableTestParallelization = true)]
