using System.Buffers.Binary;
using System.Text;
using FMRTE2008.Application;
using FMRTE2008.Domain;

namespace FMRTE2008.MemorySimulation;

/// <summary>F06: deterministic, offline mutations between separate page calls.</summary>
internal static class CrossPageSnapshotTests
{
    private enum Mutation { None, Table, Count, FirstSlot, FirstId, FirstName,
        SecondId, PartialSecondId, CancelSecondId }

    internal static async Task RunAsync()
    {
        int checks = 0;
        void Check(bool condition, string reason)
        {
            if (!condition) throw new Exception("F06: " + reason);
            checks++;
        }
        var identity = new ExecutableIdentity(SupportedBuildCatalog.Known802Sha256,
            "8.0.2", 52395, "x86");

        // Distinct calls and different page sizes must still belong to one
        // consistent session. The prior result must not alias internal buffers.
        var stableMemory = new MutableMemory();
        using (var stable = await Open(identity, stableMemory))
        {
            var first = await stable.ReadIdentityPageAsync(1, 1);
            var second = await stable.ReadIdentityPageAsync(2, 1);
            var overlapping = await stable.ReadIdentityPageAsync(1, 2);
            Check(first.Count == 1 && first[0].Id == 20, "first page incorrect");
            Check(second.Count == 1 && second[0].Id == 107, "second page incorrect");
            Check(overlapping.Count == 2 && overlapping[0] == first[0] &&
                overlapping[1] == second[0], "overlapping pages not consistent");
            Check(!ReferenceEquals(first, overlapping), "snapshot list was reused");
            Check((await stable.ReadIdentityPageAsync(3, 1)).Count == 0,
                "empty page should be stable");
            Check(first.Count == 1 && first[0].FirstName == "Ivica",
                "earlier snapshot changed after more calls");
        }
        Check(stableMemory.DisposeCount == 1, "normal session not released once");

        foreach (var mutation in new[] { Mutation.Table, Mutation.Count,
                 Mutation.FirstSlot, Mutation.FirstId, Mutation.FirstName })
        {
            var memory = new MutableMemory();
            using var session = await Open(identity, memory);
            var published = await session.ReadIdentityPageAsync(1, 1);
            memory.Change(mutation);
            await Reject<InvalidDataException>(() => session.ReadIdentityPageAsync(2, 1));
            Check(published.Count == 1 && published[0].Id == 20 &&
                published[0].FirstName == "Ivica", mutation + ": old snapshot mutated");
            Check(memory.DisposeCount == 1, mutation + ": session not invalidated");
            int reads = memory.ReadCount;
            await Reject<ObjectDisposedException>(() => session.ReadIdentityPageAsync(1, 1));
            Check(memory.ReadCount == reads, mutation + ": invalid session reused memory");

            using var fresh = await Open(identity, new MutableMemory());
            Check((await fresh.ReadIdentityPageAsync(2, 1)).Single().Id == 107,
                mutation + ": replacement session not isolated");
        }

        foreach (var mutation in new[] { Mutation.SecondId, Mutation.PartialSecondId,
                                         Mutation.CancelSecondId })
        {
            var memory = new MutableMemory();
            using var session = await Open(identity, memory);
            var published = await session.ReadIdentityPageAsync(1, 1);
            memory.Change(mutation);
            if (mutation == Mutation.CancelSecondId)
                await Reject<OperationCanceledException>(() => session.ReadIdentityPageAsync(2, 1));
            else
                await Reject<InvalidDataException>(() => session.ReadIdentityPageAsync(2, 1));
            Check(memory.DisposeCount == 1, mutation + ": failure did not close session");
            Check(published[0].Id == 20, mutation + ": previous result modified");
        }

        // Pre-cancelled second request must invalidate rather than publish
        // partial page data, while previously returned snapshots stay intact.
        var canceledMemory = new MutableMemory();
        using (var session = await Open(identity, canceledMemory))
        {
            var prior = await session.ReadIdentityPageAsync(1, 1);
            using var cts = new CancellationTokenSource();
            cts.Cancel();
            await Reject<OperationCanceledException>(
                () => session.ReadIdentityPageAsync(2, 1, cts.Token));
            Check(prior.Single().Id == 20 && canceledMemory.DisposeCount == 1,
                "cancelled cross-page call leaked state");
        }

        var denied = new FakeOpener(new MutableMemory());
        await Reject<UnauthorizedAccessException>(() =>
            VerifiedPlayerReadSession.OpenAsync(123, identity, new NoApprovedPlayerMaps(), denied));
        Check(denied.Calls == 0, "unapproved mapping opened process");
        Console.WriteLine($"OK: {checks} F06 cross-page consistency and isolation assertions (offline)");
    }

    private static Task<VerifiedPlayerReadSession> Open(
        ExecutableIdentity id, IProcessReadSession memory) =>
        VerifiedPlayerReadSession.OpenAsync(123, id,
            new FixtureCatalog(new ApprovedPlayerMap(id.Sha256, "8.0.2v1",
                0x1000, 256, "fixture-only")), new FakeOpener(memory));

    private static async Task Reject<T>(Func<Task> operation) where T : Exception
    {
        try { await operation(); }
        catch (T) { return; }
        throw new Exception("F06 expected " + typeof(T).Name);
    }

    private sealed class FixtureCatalog(ApprovedPlayerMap map) : IApprovedPlayerMapCatalog
    {
        public Task<ApprovedPlayerMap?> FindApprovedAsync(ExecutableIdentity id, CancellationToken ct) =>
            Task.FromResult<ApprovedPlayerMap?>(map);
    }

    private sealed class FakeOpener(IProcessReadSession session) : IVerifiedProcessOpener
    {
        public int Calls { get; private set; }
        public Task<IProcessReadSession> OpenVerifiedAsync(
            int pid, ExecutableIdentity id, CancellationToken ct)
        {
            Calls++;
            return Task.FromResult(session);
        }
    }

    private sealed class MutableMemory : IProcessReadSession
    {
        private readonly SimulatedProcessMemory _memory = new();
        private Mutation _mutation;
        public int ReadCount { get; private set; }
        public int DisposeCount { get; private set; }

        public MutableMemory()
        {
            _memory.AddRegion(0x1000, Header(0x2000, 2));
            var slots = new byte[8];
            BinaryPrimitives.WriteUInt32LittleEndian(slots, 0x3000);
            BinaryPrimitives.WriteUInt32LittleEndian(slots.AsSpan(4), 0x3200);
            _memory.AddRegion(0x2000, slots);
            AddPlayer(0x3000, 20, "Ivica", "Dragutinovic", 0x4000);
            AddPlayer(0x3200, 107, "Alexander", "Östlund", 0x5000);
        }

        public void Change(Mutation mutation) => _mutation = mutation;

        public async Task<byte[]> ReadAsync(nuint address, int length, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            if (DisposeCount > 0) throw new ObjectDisposedException(nameof(MutableMemory));
            ReadCount++;
            uint at = checked((uint)address);
            if (at == 0x1000 && length == 8)
            {
                if (_mutation == Mutation.Table) return Header(0x2800, 2);
                if (_mutation == Mutation.Count) return Header(0x2000, 3);
            }
            if (at == 0x2000 && length == 4 && _mutation == Mutation.FirstSlot)
                return U32(0x3500);
            if (at == 0x3100 && length == 4 && _mutation == Mutation.FirstId)
                return U32(999);
            if (at == 0x3300 && length == 4)
            {
                if (_mutation == Mutation.SecondId) return U32(999);
                if (_mutation == Mutation.PartialSecondId) return new byte[2];
                if (_mutation == Mutation.CancelSecondId)
                    throw new OperationCanceledException("F06 simulated cancellation", ct);
            }
            if (at == 0x4060 && length == 2 && _mutation == Mutation.FirstName)
                return Encoding.Unicode.GetBytes("X");
            return await _memory.ReadAsync(address, length, ct);
        }

        public void Dispose() => DisposeCount++;

        private void AddPlayer(uint address, int id, string first, string last, uint root)
        {
            var buffer = new byte[292];
            BinaryPrimitives.WriteInt32LittleEndian(buffer.AsSpan(256), id);
            BinaryPrimitives.WriteUInt32LittleEndian(buffer.AsSpan(280), root);
            BinaryPrimitives.WriteUInt32LittleEndian(buffer.AsSpan(284), root + 0x100);
            _memory.AddRegion(address, buffer);
            AddName(root, first);
            AddName(root + 0x100, last);
        }

        private void AddName(uint at, string name)
        {
            var outer = new byte[40];
            BinaryPrimitives.WriteUInt32LittleEndian(outer.AsSpan(36), at + 0x40);
            _memory.AddRegion(at, outer);
            var nested = new byte[12];
            BinaryPrimitives.WriteUInt32LittleEndian(nested.AsSpan(8), at + 0x60);
            _memory.AddRegion(at + 0x40, nested);
            _memory.AddRegion(at + 0x60, Encoding.Unicode.GetBytes(name + "\0"));
        }

        private static byte[] Header(uint table, int count)
        {
            var bytes = new byte[8];
            BinaryPrimitives.WriteUInt32LittleEndian(bytes, table);
            BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(4), count);
            return bytes;
        }
        private static byte[] U32(uint value)
        {
            var bytes = new byte[4];
            BinaryPrimitives.WriteUInt32LittleEndian(bytes, value);
            return bytes;
        }
    }
}
