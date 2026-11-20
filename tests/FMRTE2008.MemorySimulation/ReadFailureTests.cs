using System.Buffers.Binary;
using FMRTE2008.Application;
using FMRTE2008.Domain;

namespace FMRTE2008.MemorySimulation;

/// <summary>F04: deterministic read failures; fake handles only, no Windows process.</summary>
internal static class ReadFailureTests
{
    internal static async Task RunAsync()
    {
        int checks = 0;
        void Check(bool condition, string message)
        {
            if (!condition) throw new Exception("F04: " + message);
            checks++;
        }
        var identity = new ExecutableIdentity(SupportedBuildCatalog.Known802Sha256, "8.0.2", 52395, "x86");

        // One corrupt phase must poison the entire verified session, not just
        // the current record. A fresh opener/session can recover independently.
        foreach (var failure in new[] { Fault.PartialHeader, Fault.PartialSlot,
                    Fault.NullPointer, Fault.BadPointer, Fault.PartialId,
                    Fault.ProcessLost, Fault.CancelDuringRead })
        {
            var fake = new FaultSession(failure);
            var opener = new FakeOpener(fake);
            using var session = await VerifiedPlayerReadSession.OpenAsync(
                123, identity, Approved(identity), opener);
            if (failure == Fault.CancelDuringRead)
                await Reject<OperationCanceledException>(() => session.ReadFirstPlayerIdAsync());
            else if (failure == Fault.ProcessLost)
                await Reject<IOException>(() => session.ReadFirstPlayerIdAsync());
            else
                await Reject<InvalidDataException>(() => session.ReadFirstPlayerIdAsync());

            Check(fake.Disposed, $"{failure}: failed session did not release its handle");
            int reads = fake.ReadCount;
            await Reject<ObjectDisposedException>(() => session.ReadFirstPlayerIdAsync());
            Check(fake.ReadCount == reads, $"{failure}: stale session performed a new read");

            using var recovered = await VerifiedPlayerReadSession.OpenAsync(
                123, identity, Approved(identity), new FakeOpener(new FaultSession(Fault.None)));
            var player = await recovered.ReadFirstPlayerIdAsync();
            Check(player.Id == 731, $"{failure}: fresh verified session could not recover");
        }

        // Invalid pointer value near 4 GiB must reject with arithmetic overflow
        // rather than wrap back into lower memory and accidentally produce an ID.
        var overflow = new FaultSession(Fault.OverflowPointer);
        using (var session = await VerifiedPlayerReadSession.OpenAsync(
            123, identity, Approved(identity), new FakeOpener(overflow)))
        {
            await Reject<OverflowException>(() => session.ReadFirstPlayerIdAsync());
            Check(overflow.Disposed, "Overflow did not invalidate session");
            await Reject<ObjectDisposedException>(() => session.ReadFirstPlayerIdAsync());
        }

        // Cancellation between record reads in a page must not return a partial
        // page or leave the previously verified session reusable.
        var pageFault = new FaultSession(Fault.ProcessLost);
        using (var session = await VerifiedPlayerReadSession.OpenAsync(
            123, identity, Approved(identity), new FakeOpener(pageFault)))
        {
            await Reject<IOException>(() => session.ReadIdentityPageAsync(1, 1));
            Check(pageFault.Disposed, "Page failure did not release handle");
            await Reject<ObjectDisposedException>(() => session.ReadFirstPlayerIdAsync());
        }

        // An unapproved catalog must never use the fake opener either.
        var denied = new FakeOpener(new FaultSession(Fault.None));
        await Reject<UnauthorizedAccessException>(() =>
            VerifiedPlayerReadSession.OpenAsync(123, identity, new NoApprovedPlayerMaps(), denied));
        Check(denied.OpenCount == 0, "Unapproved mapping reached the opener");
        Console.WriteLine($"OK: {checks} F04 assertions for partial reads, pointers, interrupted sessions and recovery");
    }

    private static IApprovedPlayerMapCatalog Approved(ExecutableIdentity identity) =>
        new FakeCatalog(new ApprovedPlayerMap(identity.Sha256, "8.0.2v1", 0x1000, 256, "fixture-only"));

    private static async Task Reject<T>(Func<Task> action) where T : Exception
    {
        try { await action(); }
        catch (T) { return; }
        throw new Exception("F04 expected " + typeof(T).Name);
    }

    private enum Fault
    {
        None, PartialHeader, PartialSlot, NullPointer, BadPointer,
        PartialId, ProcessLost, CancelDuringRead, OverflowPointer
    }

    private sealed class FakeCatalog(ApprovedPlayerMap map) : IApprovedPlayerMapCatalog
    {
        public Task<ApprovedPlayerMap?> FindApprovedAsync(
            ExecutableIdentity identity, CancellationToken cancellationToken) =>
            Task.FromResult<ApprovedPlayerMap?>(map);
    }

    private sealed class FakeOpener(IProcessReadSession session) : IVerifiedProcessOpener
    {
        public int OpenCount { get; private set; }
        public Task<IProcessReadSession> OpenVerifiedAsync(
            int pid, ExecutableIdentity identity, CancellationToken cancellationToken)
        {
            OpenCount++;
            return Task.FromResult(session);
        }
    }

    private sealed class FaultSession(Fault fault) : IProcessReadSession
    {
        public int ReadCount { get; private set; }
        public bool Disposed { get; private set; }

        public Task<byte[]> ReadAsync(nuint address, int length, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (Disposed) throw new ObjectDisposedException(nameof(FaultSession));
            ReadCount++;
            uint at = checked((uint)address);
            if (fault == Fault.ProcessLost && ReadCount == 2)
                throw new IOException("Simulated process terminated");
            if (fault == Fault.CancelDuringRead && ReadCount == 2)
                throw new OperationCanceledException("Simulated interruption");
            if (at == 0x1000 && length == 8)
            {
                byte[] header = new byte[fault == Fault.PartialHeader ? 7 : 8];
                BinaryPrimitives.WriteUInt32LittleEndian(header, 0x2000);
                if (header.Length == 8)
                    BinaryPrimitives.WriteInt32LittleEndian(header.AsSpan(4), 1);
                return Task.FromResult(header);
            }
            if (at == 0x2000 && length == 4)
            {
                if (fault == Fault.PartialSlot)
                    return Task.FromResult(new byte[3]);
                byte[] pointer = new byte[4];
                uint playerPointer = fault switch
                {
                    Fault.NullPointer => 0,
                    Fault.BadPointer => 0x5000,
                    Fault.OverflowPointer => 0xFFFFFFFE,
                    _ => 0x3000
                };
                BinaryPrimitives.WriteUInt32LittleEndian(pointer, playerPointer);
                return Task.FromResult(pointer);
            }
            if (at == 0x3100 && length == 4)
            {
                byte[] id = new byte[fault == Fault.PartialId ? 2 : 4];
                if (id.Length == 4)
                    BinaryPrimitives.WriteInt32LittleEndian(id, 731);
                return Task.FromResult(id);
            }
            throw new InvalidDataException("F04: simulated address not mapped");
        }

        public void Dispose() => Disposed = true;
    }
}
