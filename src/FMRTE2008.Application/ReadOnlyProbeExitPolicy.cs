namespace FMRTE2008.Application;

/// <summary>
/// Exit status of a completed read-only diagnostic. Divergent samples must
/// never be reported as successful to scripts or CI consumers.
/// </summary>
public static class ReadOnlyProbeExitPolicy
{
    public const int Success = 0;
    public const int InconsistentSamples = 1;

    public static int FromStability(bool stable) =>
        stable ? Success : InconsistentSamples;
}
