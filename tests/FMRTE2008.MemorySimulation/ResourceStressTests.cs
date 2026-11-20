using System.Buffers.Binary;
using System.Text;
using FMRTE2008.Application;
using FMRTE2008.Domain;

namespace FMRTE2008.MemorySimulation;

/// <summary>F07: bounded deterministic stress; no timing sleeps, random calls or real processes.</summary>
internal static class ResourceStressTests
{
    internal static async Task RunAsync()
    {
        int checks = 0;
        void Check(bool condition, string name)
        {
            if (!condition) throw new Exception("F07: " + name);
            checks++;
        }

        var build = new ExecutableIdentity(SupportedBuildCatalog.Known802Sha256,
            "8.0.2", 52395, "x86");

        // Repeat complete and isolated sessions with varying page sizes and
        // order. No previous session state or handle is ever reused.
        for (int cycle = 0; cycle < 32; cycle++)
        {
            var memory = new TwoPlayerMemory();
            using (var session = await OpenAsync(build, memory))
            {
                var original = await session.ReadIdentityPageAsync(1, 1);
                var page2 = await session.ReadIdentityPageAsync(2, 1);
                var overlap = await session.ReadIdentityPageAsync(1, 2);
                Check(original.Count == 1 && original[0].Id == 20 &&
                    page2.Single().Id == 107 && overlap.Count == 2,
                    $"cycle {cycle}: unexpected page contents");
                Check(original[0].FirstName == "Ivica" &&
                    !ReferenceEquals(original, overlap),
                    $"cycle {cycle}: old snapshot alias");
                Check((await session.ReadIdentityPageAsync(3, 1)).Count == 0,
                    $"cycle {cycle}: out-of-range page");
            }
            Check(memory.DisposeCount == 1, $"cycle {cycle}: handle not disposed exactly once");
        }

        // Direct budget checks: no underlying read must occur after the
        // exhausted operation, and Dispose must be idempotent.
        var direct = new TwoPlayerMemory();
        var wrapper = new BudgetedReadOnlySession(direct, maxReads: 1, maxBytes: 8);
        Check((await wrapper.ReadAsync((nuint)0x1000, 8, default)).Length == 8,
            "first budgeted read failed");
        await Reject<InvalidDataException>(() =>
            wrapper.ReadAsync((nuint)0x1000, 8, default));
        Check(direct.ReadCount == 1, "read-call budget touched source twice");
        wrapper.Dispose();
        wrapper.Dispose();
        Check(direct.DisposeCount == 1, "budget wrapper double-disposed the source");
        await Reject<ObjectDisposedException>(() =>
            wrapper.ReadAsync((nuint)0x1000, 8, default));

        // Byte budget rejects the request *before* the fake handle is used.
        var bytesSource = new TwoPlayerMemory();
        using (var bytes = new BudgetedReadOnlySession(bytesSource, 100, 7))
        {
            await Reject<InvalidDataException>(() =>
                bytes.ReadAsync((nuint)0x1000, 8, default));
            Check(bytesSource.ReadCount == 0, "byte cap allowed source access");
        }
        Check(bytesSource.DisposeCount == 1, "byte-budget handle leak");

        // Exhaustion inside VerifiedPlayerReadSession triggers the same
        // fail-closed lifecycle as an incomplete read.
        foreach (var maxCalls in new[] { 1, 2, 3 })
        {
            var fake = new TwoPlayerMemory();
            using var limited = await OpenAsync(build,
                new BudgetedReadOnlySession(fake, maxReads: maxCalls, maxBytes: 4096));
            await Reject<InvalidDataException>(() => limited.ReadIdentityPageAsync(1, 1));
            Check(fake.DisposeCount == 1, $"budget {maxCalls}: session was not released");
            int priorReads = fake.ReadCount;
            await Reject<ObjectDisposedException>(() => limited.ReadIdentityPageAsync(1, 1));
            Check(fake.ReadCount == priorReads,
                $"budget {maxCalls}: invalidated session read again");
            using var recovered = await OpenAsync(build, new TwoPlayerMemory());
            Check((await recovered.ReadIdentityPageAsync(1, 1)).Single().Id == 20,
                $"budget {maxCalls}: new verified fixture session did not recover");
        }

        // Deterministic cancellation at the start of a new session should
        // release exactly one handle and never publish a partial page.
        for (int cycle = 0; cycle < 16; cycle++)
        {
            var fake = new TwoPlayerMemory();
            using var session = await OpenAsync(build, fake);
            using var cancel = new CancellationTokenSource();
            cancel.Cancel();
            await Reject<OperationCanceledException>(() =>
                session.ReadIdentityPageAsync(1, 1, cancel.Token));
            Check(fake.DisposeCount == 1 && fake.ReadCount == 0,
                $"cancel {cycle}: source accessed or not released");
            await Reject<ObjectDisposedException>(() => session.ReadIdentityPageAsync(1, 1));
        }

        var blocked = new CountingOpener(new TwoPlayerMemory());
        await Reject<UnauthorizedAccessException>(() =>
            VerifiedPlayerReadSession.OpenAsync(
                123, build, new NoApprovedPlayerMaps(), blocked));
        Check(blocked.Opens == 0, "unapproved map called process opener");

        Console.WriteLine($"OK: {checks} F07 deterministic stress, budget and recovery assertions (offline)");
    }

    private static Task<VerifiedPlayerReadSession> OpenAsync(
        ExecutableIdentity build, IProcessReadSession memory) =>
        VerifiedPlayerReadSession.OpenAsync(123, build,
            new FixtureCatalog(new ApprovedPlayerMap(build.Sha256, "8.0.2v1",
                0x1000, 256, "test-only-no-operational-approval")),
            new CountingOpener(memory));

    private static async Task Reject<T>(Func<Task> operation) where T : Exception
    {
        try { await operation(); }
        catch (T) { return; }
        throw new Exception("F07 expected " + typeof(T).Name);
    }

    private sealed class FixtureCatalog(ApprovedPlayerMap approved) : IApprovedPlayerMapCatalog
    {
        public Task<ApprovedPlayerMap?> FindApprovedAsync(ExecutableIdentity id,
            CancellationToken ct) => Task.FromResult<ApprovedPlayerMap?>(approved);
    }
    private sealed class CountingOpener(IProcessReadSession memory) : IVerifiedProcessOpener
    {
        public int Opens { get; private set; }
        public Task<IProcessReadSession> OpenVerifiedAsync(
            int pid, ExecutableIdentity id, CancellationToken ct)
        {
            Opens++;
            return Task.FromResult(memory);
        }
    }

    private sealed class TwoPlayerMemory : IProcessReadSession
    {
        private readonly SimulatedProcessMemory _memory = new();
        public int ReadCount { get; private set; }
        public int DisposeCount { get; private set; }

        public TwoPlayerMemory()
        {
            var header = new byte[8];
            BinaryPrimitives.WriteUInt32LittleEndian(header, 0x2000);
            BinaryPrimitives.WriteInt32LittleEndian(header.AsSpan(4), 2);
            _memory.AddRegion(0x1000, header);
            var slots = new byte[8];
            BinaryPrimitives.WriteUInt32LittleEndian(slots, 0x3000);
            BinaryPrimitives.WriteUInt32LittleEndian(slots.AsSpan(4), 0x3200);
            _memory.AddRegion(0x2000, slots);
            Player(0x3000, 20, "Ivica", "Dragutinovic", 0x4000);
            Player(0x3200, 107, "Alexander", "Östlund", 0x5000);
        }
        public Task<byte[]> ReadAsync(nuint address, int length, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            if (DisposeCount != 0) throw new ObjectDisposedException(nameof(TwoPlayerMemory));
            ReadCount++;
            return _memory.ReadAsync(address, length, ct);
        }
        public void Dispose() => DisposeCount++;

        private void Player(uint ptr, int id, string first, string last, uint nameRoot)
        {
            var bytes = new byte[292];
            BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(256), id);
            BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(280), nameRoot);
            BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(284), nameRoot + 0x100);
            _memory.AddRegion(ptr, bytes);
            Name(nameRoot, first);
            Name(nameRoot + 0x100, last);
        }
        private void Name(uint address, string value)
        {
            var outer = new byte[40];
            BinaryPrimitives.WriteUInt32LittleEndian(outer.AsSpan(36), address + 0x40);
            _memory.AddRegion(address, outer);
            var inner = new byte[12];
            BinaryPrimitives.WriteUInt32LittleEndian(inner.AsSpan(8), address + 0x60);
            _memory.AddRegion(address + 0x40, inner);
            _memory.AddRegion(address + 0x60,
                Encoding.Unicode.GetBytes(value + "\0"));
        }
    }
}
