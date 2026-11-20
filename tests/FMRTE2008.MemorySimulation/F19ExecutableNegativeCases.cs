using System.Buffers.Binary;
using FMRTE2008.Application;

namespace FMRTE2008.MemorySimulation;

/// <summary>
/// F19 B01: executable, individually identified negative witnesses.
/// Each case asserts an actual exception or denial. A missing rejection fails.
/// Uses only local fixtures, never opens external processes.
/// </summary>
internal static class F19ExecutableNegativeCases
{
    private static int _passed;

    internal static async Task RunAsync()
    {
        _passed = 0;
        var known = new ExecutableIdentity(SupportedBuildCatalog.Known802Sha256,
            "8.0.2", 52395, "x86");

        await Case("A01", "sha-mismatch", () =>
            Deny(!SupportedBuildCatalog.Recognize(known with
            { Sha256 = new string('0', 64) }).IsRecognized, "IsRecognized=false"));

        await Case("A02", "x86-overflow", () =>
            Reject<OverflowException>(() => Task.FromResult(
                MinimalPlayerIdentityMap.Research802V1.ResolveHeaderAddress(0xFFFFFFF0))));

        await Case("A03", "null-table-header", async () =>
        {
            var memory = new SimulatedProcessMemory();
            memory.AddRegion(0x1000, Header(0, 1));
            return await Reject<InvalidDataException>(() =>
                new PlayerListReader(memory).ReadHeaderAsync(0x1000));
        });

        await Case("A04", "invalid-player-id", async () =>
        {
            var memory = new SimulatedProcessMemory();
            memory.AddRegion(0x1000, Header(0x2000, 1));
            memory.AddRegion(0x2000, UInt(0x3000));
            var player = new byte[292]; // ID=0 is invalid
            memory.AddRegion(0x3000, player);
            var header = await new PlayerListReader(memory).ReadHeaderAsync(0x1000);
            return await Reject<InvalidDataException>(async () =>
                await new MinimalPlayerIdentityReader(memory,
                    MinimalPlayerIdentityMap.Research802V1).ReadAsync(header, 0));
        });

        await Case("A05", "unmapped-read", () =>
            Reject<InvalidDataException>(() =>
                new SimulatedProcessMemory().ReadAsync((nuint)0x5000, 4, default)));

        await Case("A06", "changed-table-during-recheck", () =>
            Reject<InvalidDataException>(() => Task.Run(() =>
                ReadOnlyRecordConsistency.ConfirmUnchanged(
                    (address, length) => address == 0x1000
                        ? Header(0x2500, 1) : UInt(0x3000),
                    0x1000, 0x2000, 1, 0, 0x3000))));

        await Case("A07", "cancelled-session-read", async () =>
        {
            using var cts = new CancellationTokenSource();
            cts.Cancel();
            return await Reject<OperationCanceledException>(() =>
                new SimulatedProcessMemory().ReadAsync((nuint)0x1000, 4, cts.Token));
        });

        await Case("A08", "live-index-source-denied", () =>
            Reject<UnauthorizedAccessException>(() =>
                Task.FromResult(new IncrementalPlayerSearch(new FakeLiveSource()))));

        await Case("A09", "deny-all-catalog", async () =>
            await Deny(await new NoApprovedPlayerMaps()
                .FindApprovedAsync(known, default) is null, "null"));

        await Case("A10", "oversized-distributed-sample", () =>
            Reject<ArgumentOutOfRangeException>(() => Task.FromResult(
                DistributedPlayerSampling.BuildIndices(3, 4))));

        await Case("A11", "wrong-version-denied", () =>
            Deny(!SupportedBuildCatalog.Recognize(known with
            { FileVersion = "8.0.1" }).IsRecognized, "IsRecognized=false"));

        await Case("A12", "no-implicit-approval", () =>
            Deny(!MinimalPlayerIdentityMap.Research802V1.IsApprovedForOperationalUse &&
                !SupportedBuildCatalog.Recognize(known).MemoryMapApproved, "false/false"));

        if (_passed != 12) throw new Exception("F19 B01 incomplete witness count");
        // This deliberately broken case is fed to the SAME executable validator.
        bool detected = false;
        try { await Reject<InvalidDataException>(() => Task.CompletedTask); }
        catch (Exception ex) when (ex is InvalidOperationException) { detected = true; }
        if (!detected) throw new Exception("F19 mutation: validator accepted omitted rejection");
        Console.WriteLine("OK F19 executable negatives: 12 individually logged A01-A12 cases; omitted rejection detected.");
    }

    private static async Task Case(string id, string name, Func<Task<string>> scenario)
    {
        string actualOutcome = await scenario();
        F20ExecutionEvidence.Record(id, name, actualOutcome);
        _passed++;
        Console.WriteLine($"OK F19 NEG {id}/{name}: expected denial observed");
    }

    private static Task<string> Deny(bool condition, string observedOutcome)
    {
        if (!condition) throw new InvalidOperationException("F19 denial was not observed");
        return Task.FromResult(observedOutcome);
    }

    private static async Task<string> Reject<T>(Func<Task> operation) where T : Exception
    {
        try { await operation(); }
        catch (T ex) when (ex.GetType() == typeof(T)) { return ex.GetType().Name; }
        throw new InvalidOperationException("F19 required exception was not raised: " + typeof(T).Name);
    }

    private static byte[] UInt(uint value)
    {
        var bytes = new byte[4];
        BinaryPrimitives.WriteUInt32LittleEndian(bytes, value);
        return bytes;
    }

    private static byte[] Header(uint table, int count)
    {
        var bytes = new byte[8];
        BinaryPrimitives.WriteUInt32LittleEndian(bytes, table);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(4), count);
        return bytes;
    }

    private sealed class FakeLiveSource : IIndexedPlayerSource
    {
        public int Count => 1;
        public bool IsLiveProcessSource => true;
        public Task<MinimalPlayerIdentity> ReadAtAsync(int index, CancellationToken ct) =>
            throw new Exception("F19 live read unexpectedly reached");
    }
}
