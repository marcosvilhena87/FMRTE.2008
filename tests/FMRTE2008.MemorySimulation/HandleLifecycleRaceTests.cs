using System.Buffers.Binary;
using FMRTE2008.Application;

namespace FMRTE2008.MemorySimulation;

/// <summary>F10: controlled handle death and lifetime races through the verified reader.</summary>
internal static class HandleLifecycleRaceTests
{
    internal static async Task RunAsync()
    {
        int checks = 0;
        void Check(bool condition, string message)
        {
            if (!condition) throw new Exception("F10 lifecycle: " + message);
            checks++;
        }

        var identity = new ExecutableIdentity(SupportedBuildCatalog.Known802Sha256,
            "8.0.2", 52395, "x86");
        var evidence = new ProcessOpeningEvidence(123, 638000000000000000, "fm.exe", identity);
        var boundIdentity = new ProcessHandleIdentity(123, evidence.StartTimeUtcTicks);
        var catalog = new FixtureCatalog(new ApprovedPlayerMap(identity.Sha256,
            "8.0.2v1", 0x1000, 256, "fixture-only"));

        async Task<VerifiedPlayerReadSession> OpenAsync(ControlledHandle handle)
        {
            var opener = new FixtureOpener(handle, evidence, identity);
            return await VerifiedPlayerReadSession.OpenAsync(123, identity, catalog, opener);
        }

        // One pending read, concurrent second read and several Dispose calls.
        // No bytes from a terminated process may escape, and the handle must
        // not be closed until the first read actually unwinds.
        var failed = new ControlledHandle(boundIdentity);
        var verified = await OpenAsync(failed);
        var pending = verified.ReadFirstPlayerIdAsync();
        await failed.Entered.Task;
        int attempts = failed.Reads;
        await Reject<InvalidOperationException>(() => verified.ReadFirstPlayerIdAsync());
        Check(failed.Reads == attempts, "concurrent read reached the handle");
        failed.Terminate();
        await Task.WhenAll(Task.Run(verified.Dispose), Task.Run(verified.Dispose));
        Check(failed.Disposes == 0, "Dispose closed a handle during in-flight read");
        failed.Release.TrySetResult(true);
        await Reject<IOException>(() => pending);
        Check(failed.Disposes == 1, "terminated handle not closed exactly once");
        await Reject<ObjectDisposedException>(() => verified.ReadFirstPlayerIdAsync());
        Check(failed.Reads == attempts, "invalidated session reused dead handle");
        verified.Dispose();
        Check(failed.Disposes == 1, "repeated Dispose released handle twice");

        // Death without Dispose: the failed read alone poisons the verified
        // session and closes the underlying handle exactly once.
        var spontaneouslyStopped = new ControlledHandle(boundIdentity);
        using (var session = await OpenAsync(spontaneouslyStopped))
        {
            var read = session.ReadFirstPlayerIdAsync();
            await spontaneouslyStopped.Entered.Task;
            spontaneouslyStopped.Terminate();
            spontaneouslyStopped.Release.TrySetResult(true);
            await Reject<IOException>(() => read);
            Check(spontaneouslyStopped.Disposes == 1,
                "death did not invalidate and close verified session");
            await Reject<ObjectDisposedException>(() => session.ReadFirstPlayerIdAsync());
        }
        Check(spontaneouslyStopped.Disposes == 1, "using Dispose doubled close");

        // Cancel while a handle read is pending. It remains leased until the
        // underlying operation completes, after which the session is poisoned.
        var canceled = new ControlledHandle(boundIdentity);
        using (var session = await OpenAsync(canceled))
        using (var token = new CancellationTokenSource())
        {
            var read = session.ReadFirstPlayerIdAsync(token.Token);
            await canceled.Entered.Task;
            token.Cancel();
            Check(canceled.Disposes == 0, "cancellation closed an active handle");
            canceled.Release.TrySetResult(true);
            await Reject<OperationCanceledException>(() => read);
            Check(canceled.Disposes == 1, "canceled read did not release handle");
            await Reject<ObjectDisposedException>(() => session.ReadFirstPlayerIdAsync());
        }

        // Independent live process session remains usable while one instance
        // at the same PID exits. Both own distinct handles and disposal state.
        var old = new ControlledHandle(boundIdentity);
        var newOne = new ControlledHandle(boundIdentity);
        using (var stale = await OpenAsync(old))
        using (var fresh = await OpenAsync(newOne))
        {
            var oldRead = stale.ReadFirstPlayerIdAsync();
            await old.Entered.Task;
            var newRead = fresh.ReadFirstPlayerIdAsync();
            await newOne.Entered.Task;
            old.Terminate();
            old.Release.TrySetResult(true);
            await Reject<IOException>(() => oldRead);
            Check(old.Disposes == 1 && newOne.Disposes == 0,
                "failed old process contaminated independent handle");
            newOne.Release.TrySetResult(true);
            Check((await newRead).Id == 731, "unaffected session did not complete");
        }
        Check(old.Disposes == 1 && newOne.Disposes == 1,
            "independent handles did not close exactly once");

        Console.WriteLine($"OK: {checks} F10 verified-handle/Dispose/concurrent-read assertions (offline)");
    }

    private static async Task Reject<T>(Func<Task> operation) where T : Exception
    {
        try { await operation(); }
        catch (T) { return; }
        throw new Exception("F10 lifecycle expected " + typeof(T).Name);
    }

    private sealed class FixtureCatalog(ApprovedPlayerMap map) : IApprovedPlayerMapCatalog
    {
        public Task<ApprovedPlayerMap?> FindApprovedAsync(ExecutableIdentity identity,
            CancellationToken cancellationToken) => Task.FromResult<ApprovedPlayerMap?>(map);
    }

    private sealed class FixtureOpener(ControlledHandle handle, ProcessOpeningEvidence evidence,
        ExecutableIdentity expected) : IVerifiedProcessOpener
    {
        public Task<IProcessReadSession> OpenVerifiedAsync(int processId,
            ExecutableIdentity identity, CancellationToken ct) =>
            VerifiedProcessOpeningGate.OpenAsync(processId, expected,
                _ => Task.FromResult(evidence),
                _ => Task.FromResult<IProcessReadSession>(handle),
                ct, requireHandleIdentity: true);
    }

    private sealed class ControlledHandle(ProcessHandleIdentity identity) : IProcessHandleBoundReadSession
    {
        private bool _terminated;
        public int Reads { get; private set; }
        public int Disposes { get; private set; }
        public TaskCompletionSource<bool> Entered { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource<bool> Release { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public ProcessHandleIdentity GetLiveHandleIdentity()
        {
            if (Disposes != 0) throw new ObjectDisposedException(nameof(ControlledHandle));
            if (_terminated) throw new IOException("Fixture process terminated");
            return identity;
        }

        public void Terminate() => _terminated = true;

        public async Task<byte[]> ReadAsync(nuint address, int length, CancellationToken ct)
        {
            GetLiveHandleIdentity();
            Reads++;
            if ((uint)address == 0x1000)
            {
                Entered.TrySetResult(true);
                await Release.Task;
            }
            ct.ThrowIfCancellationRequested();
            GetLiveHandleIdentity();
            var data = new byte[length];
            if ((uint)address == 0x1000 && length == 8)
            {
                BinaryPrimitives.WriteUInt32LittleEndian(data, 0x2000);
                BinaryPrimitives.WriteInt32LittleEndian(data.AsSpan(4), 1);
            }
            else if ((uint)address == 0x2000 && length == 4)
                BinaryPrimitives.WriteUInt32LittleEndian(data, 0x3000);
            else if ((uint)address == 0x3100 && length == 4)
                BinaryPrimitives.WriteInt32LittleEndian(data, 731);
            else
                throw new InvalidDataException("Unknown fixture address");
            return data;
        }

        public void Dispose() => Disposes++;
    }
}
