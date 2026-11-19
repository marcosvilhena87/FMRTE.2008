namespace FMRTE2008.Application;

/// <summary>Deterministic read-only index plan, independent of process access.</summary>
public static class DistributedPlayerSampling
{
    public static int[] BuildIndices(int count, int requested)
    {
        if (count < 2 || count > PlayerListReader.MaxFixtureEntries)
            throw new ArgumentOutOfRangeException(nameof(count));
        if (requested < 2 || requested > 20 || requested > count)
            throw new ArgumentOutOfRangeException(nameof(requested));
        var positions = Enumerable.Range(0, requested)
            .Select(i => (int)((long)i * (count - 1) / (requested - 1)))
            .ToArray();
        if (positions.Distinct().Count() != requested ||
            positions[0] != 0 || positions[^1] != count - 1)
            throw new InvalidDataException("Índices da amostra não são válidos");
        return positions;
    }
}
