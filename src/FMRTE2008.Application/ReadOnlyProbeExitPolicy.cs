namespace FMRTE2008.Application;

/// <summary>
/// Snapshot of one player observed by the isolated read-only diagnostic.
/// </summary>
public sealed record ReadOnlyProbePlayer(uint Pointer, int Id, string First, string Last, string Common);

/// <summary>
/// Diagnostic-only sample. This type does not approve maps or open processes.
/// </summary>
public sealed record ReadOnlyProbeSample(
    uint Table, int Count, IReadOnlyList<ReadOnlyProbePlayer> Records);

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

    public static int FromSamples(IReadOnlyList<ReadOnlyProbeSample> samples) =>
        FromStability(AreStable(samples));

    public static bool AreStable(IReadOnlyList<ReadOnlyProbeSample> samples)
    {
        ArgumentNullException.ThrowIfNull(samples);
        if (samples.Count == 0)
            return false;

        var first = samples[0];
        if (first is null || first.Records is null)
            return false;

        for (int i = 1; i < samples.Count; i++)
        {
            var next = samples[i];
            if (next is null || next.Records is null ||
                first.Table != next.Table ||
                first.Count != next.Count ||
                first.Records.Count != next.Records.Count)
                return false;

            for (int j = 0; j < first.Records.Count; j++)
            {
                if (first.Records[j] is null || next.Records[j] is null ||
                    first.Records[j] != next.Records[j])
                    return false;
            }
        }
        return true;
    }
}
