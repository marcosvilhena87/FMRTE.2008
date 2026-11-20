using FMRTE2008.Application;

namespace FMRTE2008.MemorySimulation;

/// <summary>
/// F19: deterministic, offline negative controls for the A01-A12 approval gate.
/// No real process, native handle, map approval, network or game memory access.
/// </summary>
internal static class F19ApprovalGateTests
{
    internal static async Task RunAsync()
    {
        int checks = 0;
        void Check(bool good, string label)
        {
            if (!good) throw new Exception("F19 approval gate: " + label);
            checks++;
        }

        var known = new ExecutableIdentity(SupportedBuildCatalog.Known802Sha256,
            "8.0.2", 52395, "x86");
        var research = MinimalPlayerIdentityMap.Research802V1;
        Check(SupportedBuildCatalog.Recognize(known).IsRecognized, "known identity");
        Check(!SupportedBuildCatalog.Recognize(known).MemoryMapApproved, "recognized is not approved");
        Check(!research.IsApprovedForOperationalUse, "research map not operational");
        foreach (var invalid in new[]
        {
            known with { Sha256 = new string('0', 64) },
            known with { Architecture = "x64" },
            known with { FileVersion = "8.0.1" },
            known with { FilePrivatePart = 52396 },
            known with { Sha256 = "not-a-valid-sha256" }
        })
        {
            Check(!SupportedBuildCatalog.Recognize(invalid).IsRecognized,
                "reject identity mismatch");
            Check(!research.Matches(invalid), "reject research map mismatch");
        }

        Check(research.ResolveHeaderAddress(0x00400000) == 0x01B1ED4C,
            "known base + RVA only (not real relocation)");
        Check(research.ResolveHeaderAddress(0x08000000) == 0x0971ED4C,
            "synthetic relocated base + RVA");
        Reject<ArgumentOutOfRangeException>(() => research.ResolveHeaderAddress(0));
        checks++;
        Reject<OverflowException>(() => research.ResolveHeaderAddress(0xFFFFFFF0));
        checks++;

        foreach (int count in new[] { 5154, 6697, 6698 })
        {
            int[] sample = DistributedPlayerSampling.BuildIndices(count, 9);
            Check(sample.Length == 9 && sample[0] == 0 && sample[^1] == count - 1 &&
                sample.SequenceEqual(sample.Distinct().OrderBy(i => i)),
                "distributed indices remain in-bounds and distinct");
        }
        Reject<ArgumentOutOfRangeException>(() => DistributedPlayerSampling.BuildIndices(1, 9));
        checks++;
        Reject<ArgumentOutOfRangeException>(() => DistributedPlayerSampling.BuildIndices(5154, 21));
        checks++;

        // A09/A12: the sole production catalog must refuse every proposed identity
        // before any opener delegate is called, even for a recognized executable.
        var catalog = new NoApprovedPlayerMaps();
        int acquisitions = 0;
        var opener = new NeverAcquire(() => acquisitions++);
        foreach (var identity in new[]
        {
            known, known with { Sha256 = new string('0', 64) },
            known with { Architecture = "x64" }
        })
        {
            Check(await catalog.FindApprovedAsync(identity, CancellationToken.None) is null,
                "deny-all catalog");
            await RejectAsync<UnauthorizedAccessException>(() =>
                VerifiedPlayerReadSession.OpenAsync(123, identity, catalog, opener));
            checks++;
            Check(acquisitions == 0, "unapproved identity never reaches opener");
        }

        // Invalid PID must be rejected even before catalog lookup / opener.
        await RejectAsync<ArgumentOutOfRangeException>(() =>
            VerifiedPlayerReadSession.OpenAsync(0, known, catalog, opener));
        checks++;
        Check(acquisitions == 0, "invalid PID cannot reach opener");

        // A12 remains an external, explicit human decision: all these fixtures
        // are passing while approval is STILL false. Do not add a fixture
        // catalog here that would blur production vs. simulated authorization.
        Check(!research.IsApprovedForOperationalUse &&
            !SupportedBuildCatalog.Recognize(known).MemoryMapApproved,
            "successful tests never grant approval");
        Console.WriteLine($"OK F19 approval gate: {checks} offline deterministic assertions, A01/A02/A10/A11/A12 and deny-all A09; approved=false operational=false.");
    }

    private static void Reject<T>(Action run) where T : Exception
    {
        try { run(); }
        catch (T ex) when (ex.GetType() == typeof(T)) { return; }
        throw new Exception("F19 expected rejection: " + typeof(T).Name);
    }

    private static async Task RejectAsync<T>(Func<Task> run) where T : Exception
    {
        try { await run(); }
        catch (T ex) when (ex.GetType() == typeof(T)) { return; }
        throw new Exception("F19 expected rejection: " + typeof(T).Name);
    }

    private sealed class NeverAcquire(Action onAcquire) : IVerifiedProcessOpener
    {
        public Task<IProcessReadSession> OpenVerifiedAsync(int pid,
            ExecutableIdentity identity, CancellationToken ct)
        {
            onAcquire();
            throw new Exception("F19 must never acquire a live source");
        }
    }
}
