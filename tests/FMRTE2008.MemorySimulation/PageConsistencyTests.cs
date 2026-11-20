using System.Buffers.Binary;
using System.Text;
using FMRTE2008.Application;
using FMRTE2008.Domain;

namespace FMRTE2008.MemorySimulation;

/// <summary>Deterministic F05 mutations, all from an injected fictional reader.</summary>
internal static class PageConsistencyTests
{
    private enum Fault { None, TableAfterFirstId, CountAfterFirstId,
        SecondSlotAfterFirstId, FirstSlotAfterLastId, PartialLastId,
        CancelLastId, DuplicateId, DuplicatePointer,
        SecondSlotDuringFinalSweep, FirstSlotAfterSecondFinalCheck,
        PartialFinalSweep, CancelFinalSweep }

    public static async Task RunAsync()
    {
        int passed = 0;
        void Check(bool valid, string message)
        {
            if (!valid) throw new Exception("F05: " + message);
            passed++;
        }
        var identity = new ExecutableIdentity(SupportedBuildCatalog.Known802Sha256,
            "8.0.2", 52395, "x86");

        var stable = new FaultMemory(Fault.None);
        using (var session = await Open(identity, stable))
        {
            var first = await session.ReadIdentityPageAsync(1, 2);
            Check(first.Count == 2 && first[0].Id == 20 && first[1].Id == 107,
                "Stable page not returned");
            Check(first[0].LastName == "Dragutinovic" && first[1].LastName == "Östlund",
                "Stable names not decoded");
            Check((await session.ReadIdentityPageAsync(2, 2)).Count == 0,
                "Empty page not returned");
        }
        Check(stable.Disposed, "Stable session not disposed");

        foreach (var fault in new[] { Fault.TableAfterFirstId, Fault.CountAfterFirstId,
                     Fault.SecondSlotAfterFirstId, Fault.FirstSlotAfterLastId,
                     Fault.PartialLastId, Fault.CancelLastId, Fault.DuplicateId,
                     Fault.DuplicatePointer, Fault.SecondSlotDuringFinalSweep,
                     Fault.FirstSlotAfterSecondFinalCheck, Fault.PartialFinalSweep,
                     Fault.CancelFinalSweep })
        {
            var fake = new FaultMemory(fault);
            using var session = await Open(identity, fake);
            if (fault is Fault.CancelLastId or Fault.CancelFinalSweep)
                await Reject<OperationCanceledException>(() => session.ReadIdentityPageAsync(1, 2));
            else
                await Reject<InvalidDataException>(() => session.ReadIdentityPageAsync(1, 2));
            Check(fake.Disposed, fault + ": session handle was not released");
            int reads = fake.ReadCount;
            await Reject<ObjectDisposedException>(() => session.ReadIdentityPageAsync(1, 2));
            Check(fake.ReadCount == reads, fault + ": invalidated session was read again");

            // Recovery is a *new* verified fixture session, never a retry of
            // the corrupt page or its previous process handle.
            using var replacement = await Open(identity, new FaultMemory(Fault.None));
            Check((await replacement.ReadIdentityPageAsync(1, 2)).Count == 2,
                fault + ": fresh session failed");
        }

        var denied = new CountingOpener(new FaultMemory(Fault.None));
        await Reject<UnauthorizedAccessException>(() =>
            VerifiedPlayerReadSession.OpenAsync(123, identity, new NoApprovedPlayerMaps(), denied));
        Check(denied.OpenCount == 0, "Unapproved map reached opener");

        Console.WriteLine($"OK: {passed} F05 page consistency and fail-closed assertions (offline)");
    }

    private static async Task<VerifiedPlayerReadSession> Open(
        ExecutableIdentity id, IProcessReadSession memory) =>
        await VerifiedPlayerReadSession.OpenAsync(123, id,
            new FixtureCatalog(new ApprovedPlayerMap(id.Sha256,
                "8.0.2v1", 0x1000, 256, "fixture-only")),
            new CountingOpener(memory));

    private static async Task Reject<T>(Func<Task> action) where T : Exception
    {
        try { await action(); }
        catch (T) { return; }
        throw new Exception("F05 expected " + typeof(T).Name);
    }

    private sealed class FixtureCatalog(ApprovedPlayerMap map) : IApprovedPlayerMapCatalog
    {
        public Task<ApprovedPlayerMap?> FindApprovedAsync(ExecutableIdentity id, CancellationToken ct) =>
            Task.FromResult<ApprovedPlayerMap?>(map);
    }
    private sealed class CountingOpener(IProcessReadSession session) : IVerifiedProcessOpener
    {
        public int OpenCount { get; private set; }
        public Task<IProcessReadSession> OpenVerifiedAsync(int pid, ExecutableIdentity id,
            CancellationToken ct)
        {
            OpenCount++;
            return Task.FromResult(session);
        }
    }

    private sealed class FaultMemory : IProcessReadSession
    {
        private readonly SimulatedProcessMemory _memory = new();
        private readonly Fault _fault;
        private bool _readFirstId, _readLastId;
        private int _secondSlotReads;
        public bool Disposed { get; private set; }
        public int ReadCount { get; private set; }

        public FaultMemory(Fault fault)
        {
            _fault = fault;
            _memory.AddRegion(0x1000, Header(0x2000, 2));
            var pointers = new byte[8];
            BinaryPrimitives.WriteUInt32LittleEndian(pointers, 0x3000);
            BinaryPrimitives.WriteUInt32LittleEndian(pointers.AsSpan(4), 0x3200);
            _memory.AddRegion(0x2000, pointers);
            Player(0x3000, 20, "Ivica", "Dragutinovic", 0x4000);
            Player(0x3200, 107, "Alexander", "Östlund", 0x5000);
        }

        public async Task<byte[]> ReadAsync(nuint address, int length, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            if (Disposed) throw new ObjectDisposedException(nameof(FaultMemory));
            ReadCount++;
            uint at = checked((uint)address);
            if (at == 0x3100 && length == 4) _readFirstId = true;
            if (at == 0x3300 && length == 4)
            {
                _readLastId = true;
                if (_fault == Fault.CancelLastId)
                    throw new OperationCanceledException("F05 simulated mid-page cancellation", ct);
                if (_fault == Fault.PartialLastId) return new byte[2];
                if (_fault == Fault.DuplicateId) return U32(20);
            }

            if (at == 0x1000 && length == 8 && _readFirstId)
            {
                if (_fault == Fault.TableAfterFirstId) return Header(0x2800, 2);
                if (_fault == Fault.CountAfterFirstId) return Header(0x2000, 3);
            }
            if (at == 0x2004 && length == 4)
            {
                _secondSlotReads++;
                // At read #4, initial capture, decoder lookup and per-record
                // check are complete; the final forward sweep is underway.
                if (_secondSlotReads == 4)
                {
                    if (_fault == Fault.SecondSlotDuringFinalSweep)
                        return U32(0x3500);
                    if (_fault == Fault.PartialFinalSweep)
                        return new byte[2];
                    if (_fault == Fault.CancelFinalSweep)
                        throw new OperationCanceledException("F05 simulated final-sweep cancellation", ct);
                }
            }
            if (at == 0x2004 && length == 4 && _readFirstId &&
                _fault == Fault.SecondSlotAfterFirstId) return U32(0x3500);
            // After the second slot is confirmed in the forward sweep,
            // mutate the first slot that the forward sweep already visited.
            // The reverse sweep must detect this intervening change.
            if (at == 0x2000 && length == 4 &&
                _fault == Fault.FirstSlotAfterSecondFinalCheck &&
                _secondSlotReads >= 4)
                return U32(0x3600);
            if (at == 0x2000 && length == 4 && _readLastId &&
                _fault == Fault.FirstSlotAfterLastId) return U32(0x3600);
            if (at == 0x2004 && length == 4 &&
                _fault == Fault.DuplicatePointer) return U32(0x3000);
            return await _memory.ReadAsync(address, length, ct);
        }
        public void Dispose() => Disposed = true;

        private void Player(uint address, int id, string first, string last, uint root)
        {
            var player = new byte[292];
            BinaryPrimitives.WriteInt32LittleEndian(player.AsSpan(256), id);
            BinaryPrimitives.WriteUInt32LittleEndian(player.AsSpan(280), root);
            BinaryPrimitives.WriteUInt32LittleEndian(player.AsSpan(284), root + 0x100);
            _memory.AddRegion(address, player);
            Name(root, first);
            Name(root + 0x100, last);
        }
        private void Name(uint address, string name)
        {
            var outer = new byte[40];
            BinaryPrimitives.WriteUInt32LittleEndian(outer.AsSpan(36), address + 0x40);
            _memory.AddRegion(address, outer);
            var inner = new byte[12];
            BinaryPrimitives.WriteUInt32LittleEndian(inner.AsSpan(8), address + 0x60);
            _memory.AddRegion(address + 0x40, inner);
            _memory.AddRegion(address + 0x60, Encoding.Unicode.GetBytes(name + "\0"));
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
