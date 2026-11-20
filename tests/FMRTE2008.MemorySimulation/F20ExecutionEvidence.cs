namespace FMRTE2008.MemorySimulation;

/// <summary>F20 offline execution journal. Each record is emitted only after a
/// real negative assertion has succeeded. Never approves a mapping.</summary>
internal static class F20ExecutionEvidence
{
    internal sealed record Observed(string Criterion, string CaseId, string ActualOutcome);
    private static readonly List<Observed> Records = new();

    internal static void Reset() => Records.Clear();
    internal static void Record(string criterion, string caseId, string actualOutcome)
    {
        if (string.IsNullOrWhiteSpace(criterion) || string.IsNullOrWhiteSpace(caseId) ||
            string.IsNullOrWhiteSpace(actualOutcome) ||
            Records.Any(r => r.Criterion == criterion || r.CaseId == caseId))
            throw new InvalidDataException("F20 missing/duplicate execution evidence");
        Records.Add(new(criterion, caseId, actualOutcome));
    }

    internal static IReadOnlyList<Observed> Snapshot() => Records.ToArray();
}
