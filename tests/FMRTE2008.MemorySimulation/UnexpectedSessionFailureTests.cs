using System.Buffers.Binary;
using FMRTE2008.Application;
using FMRTE2008.Domain;

namespace FMRTE2008.MemorySimulation;

/// <summary>F08: unexpected provider faults never leave a verified session reusable.</summary>
internal static class UnexpectedSessionFailureTests
{
    internal static async Task RunAsync()
    {
        int assertions = 0;
        void Check(bool condition, string detail)
        {
            if (!condition) throw new Exception("F08: " + detail);
            assertions++;
        }

        var identity = new ExecutableIdentity(SupportedBuildCatalog.Known802Sha256,
            "8.0.2", 52395, "x86");
        var map = new FixtureCatalog(new ApprovedPlayerMap(identity.Sha256,
            "8.0.2v1", 0x1000, 256, "fixture-only"));

        foreach (var kind in new[] { FailureKind.InvalidOperation, FailureKind.NotSupported,
                     FailureKind.Generic })
        {
            var faulty = new ControlledSource(kind);
            using var session = await VerifiedPlayerReadSession.OpenAsync(
                123, identity, map, new FixtureOpener(faulty));

            await Reject<Exception>(() => session.ReadFirstPlayerIdAsync(),
                ex => kind switch
                {
                    FailureKind.InvalidOperation => ex is InvalidOperationException,
                    FailureKind.NotSupported => ex is NotSupportedException,
                    _ => ex.GetType() == typeof(Exception)
                });
            Check(faulty.Disposes == 1, kind + ": failed source not disposed once");
            int attempts = faulty.Reads;
            await Reject<ObjectDisposedException>(() => session.ReadFirstPlayerIdAsync());
            Check(faulty.Reads == attempts, kind + ": poisoned session accessed source");
            session.Dispose();
            Check(faulty.Disposes == 1, kind + ": double dispose");

            var fresh = new ControlledSource(FailureKind.None);
            using (var recovered = await VerifiedPlayerReadSession.OpenAsync(
                123, identity, map, new FixtureOpener(fresh)))
            {
                Check((await recovered.ReadFirstPlayerIdAsync()).Id == 731,
                    kind + ": fresh session could not recover");
            }
            Check(fresh.Disposes == 1, kind + ": recovered handle leak");
        }

        // Two separate sessions may never reuse one another's logical state.
        var first = new ControlledSource(FailureKind.InvalidOperation);
        var second = new ControlledSource(FailureKind.None);
        using var stale = await VerifiedPlayerReadSession.OpenAsync(
            123, identity, map, new FixtureOpener(first));
        using var independent = await VerifiedPlayerReadSession.OpenAsync(
            124, identity, map, new FixtureOpener(second));
        await Reject<InvalidOperationException>(() => stale.ReadFirstPlayerIdAsync());
        Check((await independent.ReadFirstPlayerIdAsync()).Id == 731,
            "failure in one session affected independent session");
        Check(first.Disposes == 1 && second.Disposes == 0,
            "session handles were shared");

        var blocked = new FixtureOpener(new ControlledSource(FailureKind.None));
        await Reject<UnauthorizedAccessException>(() => VerifiedPlayerReadSession.OpenAsync(
            123, identity, new NoApprovedPlayerMaps(), blocked));
        Check(blocked.Opens == 0, "unapproved map reached opener");

        Console.WriteLine($"OK: {assertions} F08 unexpected-failure/session-isolation assertions (offline)");
    }

    private static async Task Reject<T>(Func<Task> operation,
        Func<Exception, bool>? predicate = null) where T : Exception
    {
        try { await operation(); }
        catch (T ex) when (predicate is null || predicate(ex)) { return; }
        throw new Exception("F08 expected " + typeof(T).Name);
    }

    private enum FailureKind { None, InvalidOperation, NotSupported, Generic }

    private sealed class FixtureCatalog(ApprovedPlayerMap map) : IApprovedPlayerMapCatalog
    {
        public Task<ApprovedPlayerMap?> FindApprovedAsync(ExecutableIdentity identity,
            CancellationToken ct) => Task.FromResult<ApprovedPlayerMap?>(map);
    }

    private sealed class FixtureOpener(IProcessReadSession memory) : IVerifiedProcessOpener
    {
        public int Opens { get; private set; }
        public Task<IProcessReadSession> OpenVerifiedAsync(int pid, ExecutableIdentity identity,
            CancellationToken ct)
        {
            Opens++;
            return Task.FromResult(memory);
        }
    }

    private sealed class ControlledSource(FailureKind failure) : IProcessReadSession
    {
        public int Reads { get; private set; }
        public int Disposes { get; private set; }

        public Task<byte[]> ReadAsync(nuint address, int length, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            if (Disposes > 0) throw new ObjectDisposedException(nameof(ControlledSource));
            Reads++;
            if (failure != FailureKind.None)
                throw failure switch
                {
                    FailureKind.InvalidOperation => new InvalidOperationException("Injected provider failure"),
                    FailureKind.NotSupported => new NotSupportedException("Injected provider failure"),
                    _ => new Exception("Injected unexpected provider failure")
                };

            uint at = checked((uint)address);
            var bytes = new byte[length];
            if (at == 0x1000 && length == 8)
            {
                BinaryPrimitives.WriteUInt32LittleEndian(bytes, 0x2000);
                BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(4), 1);
            }
            else if (at == 0x2000 && length == 4)
                BinaryPrimitives.WriteUInt32LittleEndian(bytes, 0x3000);
            else if (at == 0x3100 && length == 4)
                BinaryPrimitives.WriteInt32LittleEndian(bytes, 731);
            else
                throw new InvalidDataException("Unexpected fixture address");
            return Task.FromResult(bytes);
        }

        public void Dispose() => Disposes++;
    }
}
