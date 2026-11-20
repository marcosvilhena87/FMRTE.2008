using System.Buffers.Binary;
using FMRTE2008.Application;

namespace FMRTE2008.MemorySimulation;

/// <summary>F04 lifecycle interleavings using exclusively fake, read-only sessions.</summary>
internal static class DisposeRaceTests
{
    internal static async Task RunAsync()
    {
        int checks = 0;
        void Check(bool ok, string description)
        {
            if (!ok) throw new Exception("F04 Dispose race: " + description);
            checks++;
        }
        var identity = new ExecutableIdentity(
            SupportedBuildCatalog.Known802Sha256, "8.0.2", 52395, "x86");

        // Dispose during a pending read must not close the underlying handle
        // early, nor deliver the completed record after the session is closed.
        var pending = new PausedSession();
        var verified = await OpenAsync(identity, pending);
        var read = verified.ReadFirstPlayerIdAsync();
        await pending.Entered.Task;
        verified.Dispose();
        Check(pending.DisposeCount == 0, "Handle closed during in-flight ReadAsync");
        await Reject<ObjectDisposedException>(() => verified.ReadFirstPlayerIdAsync());
        await Reject<ObjectDisposedException>(() => verified.ReadIdentityPageAsync(1));
        pending.Release.TrySetResult(true);
        await Reject<ObjectDisposedException>(() => read);
        Check(pending.DisposeCount == 1, "Handle not closed once after read completion");
        verified.Dispose();
        Check(pending.DisposeCount == 1, "Double dispose after pending operation");

        // A competing read must be denied while the first operation is pending.
        var competing = new PausedSession();
        using (var session = await OpenAsync(identity, competing))
        {
            var first = session.ReadFirstPlayerIdAsync();
            await competing.Entered.Task;
            int reads = competing.ReadCount;
            await Reject<InvalidOperationException>(() => session.ReadFirstPlayerIdAsync());
            Check(competing.ReadCount == reads, "Overlapping read touched memory");
            competing.Release.TrySetResult(true);
            var player = await first;
            Check(player.Id == 731, "First uncontended read did not finish");
        }
        Check(competing.DisposeCount == 1, "Handle not disposed after normal completion");

        // Repeated concurrent Dispose calls are idempotent even during a read.
        var repeated = new PausedSession();
        var repeatedSession = await OpenAsync(identity, repeated);
        var underway = repeatedSession.ReadFirstPlayerIdAsync();
        await repeated.Entered.Task;
        await Task.WhenAll(
            Task.Run(repeatedSession.Dispose), Task.Run(repeatedSession.Dispose));
        Check(repeated.DisposeCount == 0, "Parallel Dispose closed active handle");
        repeated.Release.TrySetResult(true);
        await Reject<ObjectDisposedException>(() => underway);
        Check(repeated.DisposeCount == 1, "Parallel Dispose released handle more than once");

        // A cancellation after the first read has begun also invalidates
        // the session and defers the close until the read path unwinds.
        var canceled = new PausedSession();
        using (var session = await OpenAsync(identity, canceled))
        {
            using var cts = new CancellationTokenSource();
            var operation = session.ReadFirstPlayerIdAsync(cts.Token);
            await canceled.Entered.Task;
            cts.Cancel();
            canceled.Release.TrySetResult(true);
            await Reject<OperationCanceledException>(() => operation);
            Check(canceled.DisposeCount == 1, "Cancellation did not release handle");
            await Reject<ObjectDisposedException>(() => session.ReadFirstPlayerIdAsync());
        }
        Console.WriteLine($"OK: {checks} F04 deterministic Dispose/read race assertions");
    }

    private static async Task<VerifiedPlayerReadSession> OpenAsync(
        ExecutableIdentity identity, IProcessReadSession memory) =>
        await VerifiedPlayerReadSession.OpenAsync(123, identity,
            new FakeCatalog(new ApprovedPlayerMap(identity.Sha256, "8.0.2v1",
                0x1000, 256, "fixture-only")),
            new FakeOpener(memory));

    private static async Task Reject<T>(Func<Task> action) where T : Exception
    {
        try { await action(); }
        catch (T) { return; }
        throw new Exception("Expected " + typeof(T).Name);
    }

    private sealed class FakeCatalog(ApprovedPlayerMap map) : IApprovedPlayerMapCatalog
    {
        public Task<ApprovedPlayerMap?> FindApprovedAsync(
            ExecutableIdentity identity, CancellationToken ct) =>
            Task.FromResult<ApprovedPlayerMap?>(map);
    }
    private sealed class FakeOpener(IProcessReadSession session) : IVerifiedProcessOpener
    {
        public Task<IProcessReadSession> OpenVerifiedAsync(
            int pid, ExecutableIdentity identity, CancellationToken ct) =>
            Task.FromResult(session);
    }

    private sealed class PausedSession : IProcessReadSession
    {
        public TaskCompletionSource<bool> Entered { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource<bool> Release { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        public int DisposeCount { get; private set; }
        public int ReadCount { get; private set; }

        public async Task<byte[]> ReadAsync(
            nuint address, int length, CancellationToken cancellationToken)
        {
            if (DisposeCount != 0)
                throw new ObjectDisposedException(nameof(PausedSession));
            ReadCount++;
            if ((uint)address == 0x1000)
            {
                Entered.TrySetResult(true);
                await Release.Task; // explicit test-controlled interleaving
                byte[] header = new byte[8];
                BinaryPrimitives.WriteUInt32LittleEndian(header, 0x2000);
                BinaryPrimitives.WriteInt32LittleEndian(header.AsSpan(4), 1);
                return header;
            }
            byte[] bytes = new byte[4];
            if ((uint)address == 0x2000)
                BinaryPrimitives.WriteUInt32LittleEndian(bytes, 0x3000);
            else if ((uint)address == 0x3100)
                BinaryPrimitives.WriteInt32LittleEndian(bytes, 731);
            else
                throw new InvalidDataException("Unexpected test address");
            return bytes;
        }

        public void Dispose() => DisposeCount++;
    }
}
