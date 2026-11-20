using FMRTE2008.Application;

namespace FMRTE2008.MemorySimulation;

/// <summary>F10: kernel-handle identity contract exercised with fake sources only.</summary>
internal static class HandleBindingTests
{
    internal static async Task RunAsync()
    {
        int checks = 0;
        void Check(bool ok, string why)
        {
            if (!ok) throw new Exception("F10: " + why);
            checks++;
        }
        var expected = new ExecutableIdentity(SupportedBuildCatalog.Known802Sha256,
            "8.0.2", 52395, "x86");
        var evidence = new ProcessOpeningEvidence(123, 638000000000000000,
            "fm.exe", expected);
        var identity = new ProcessHandleIdentity(123, evidence.StartTimeUtcTicks);

        Task<ProcessOpeningEvidence> Inspect(CancellationToken _) =>
            Task.FromResult(evidence);
        Task<IProcessReadSession> Acquire(IProcessReadSession source, CancellationToken _) =>
            Task.FromResult(source);
        Task<IProcessReadSession> Open(IProcessReadSession source,
            Func<CancellationToken, Task<ProcessOpeningEvidence>>? inspect = null,
            CancellationToken token = default) =>
            VerifiedProcessOpeningGate.OpenAsync(123, expected, inspect ?? Inspect,
                ct => Acquire(source, ct), token, requireHandleIdentity: true);

        var accepted = new FakeBoundSession(identity);
        using (var opened = await Open(accepted))
        {
            Check(ReferenceEquals(opened, accepted), "accepted wrong handle");
            Check(accepted.Inspections == 2, "bound handle not checked before and after PID inspection");
            Check(accepted.Disposes == 0, "valid handle prematurely closed");
        }
        Check(accepted.Disposes == 1, "valid handle was not released");

        foreach (var bad in new[]
        {
            new ProcessHandleIdentity(124, identity.StartTimeUtcTicks),
            new ProcessHandleIdentity(123, identity.StartTimeUtcTicks + 1)
        })
        {
            var handle = new FakeBoundSession(bad);
            await Reject<InvalidOperationException>(() => Open(handle));
            Check(handle.Disposes == 1 && handle.Inspections == 1,
                "wrong kernel instance was not rejected before second PID check");
        }

        var unbound = new FakeUnboundSession();
        await Reject<UnauthorizedAccessException>(() => Open(unbound));
        Check(unbound.Disposes == 1, "non-bound handle was not rejected and closed");

        var exited = new FakeBoundSession(identity) { ExitOnInspection = 1 };
        await Reject<InvalidOperationException>(() => Open(exited));
        Check(exited.Disposes == 1, "exited handle leaked");

        var changed = new FakeBoundSession(identity) { RecycleOnInspection = 2 };
        await Reject<InvalidOperationException>(() => Open(changed));
        Check(changed.Disposes == 1, "handle switched identity after second inspection");

        var secondEntered = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var secondRelease = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var pending = new FakeBoundSession(identity);
        var attempts = 0;
        async Task<ProcessOpeningEvidence> PausedInspect(CancellationToken token)
        {
            if (++attempts == 1) return evidence;
            secondEntered.TrySetResult(true);
            await secondRelease.Task;
            return evidence;
        }
        using (var cancellation = new CancellationTokenSource())
        {
            var operation = Open(pending, PausedInspect, cancellation.Token);
            await secondEntered.Task;
            Check(pending.Disposes == 0, "handle closed during in-flight inspection");
            cancellation.Cancel();
            secondRelease.TrySetResult(true);
            await Reject<OperationCanceledException>(() => operation);
            Check(pending.Disposes == 1, "cancel during inspection leaked handle");
        }

        // Reused PID with a newly created process must not receive a prior handle.
        var old = new FakeBoundSession(identity);
        var newProcess = new FakeBoundSession(identity with
        {
            StartTimeUtcTicks = identity.StartTimeUtcTicks + 1
        });
        using (var current = await Open(old))
        {
            await Reject<InvalidOperationException>(() => Open(newProcess));
            Check(old.Disposes == 0 && newProcess.Disposes == 1,
                "recycled PID affected an independent open handle");
        }
        Check(old.Disposes == 1, "old handle leaked");

        // Read interruption: a fake process exits during an in-flight read;
        // no data may be returned from a completed but invalidated source.
        var stopped = new FakeBoundSession(identity);
        using (var live = await Open(stopped))
        {
            stopped.ExitDuringNextRead = true;
            await Reject<InvalidOperationException>(() =>
                live.ReadAsync((nuint)0x1000, 8, default));
            Check(stopped.Published == 0, "published bytes after simulated termination");
        }
        Check(stopped.Disposes == 1, "terminated handle was not released");

        var recovered = new FakeBoundSession(identity);
        using (var fresh = await Open(recovered))
            Check((await fresh.ReadAsync((nuint)0x1000, 8, default)).Length == 8,
                "independent re-open did not recover");
        Check(recovered.Disposes == 1, "reopened handle leaked");

        Console.WriteLine($"OK: {checks} F10 handle-binding/exit/reuse assertions (offline)");
    }

    private static async Task Reject<T>(Func<Task> action) where T : Exception
    {
        try { await action(); }
        catch (T) { return; }
        throw new Exception("F10 expected " + typeof(T).Name);
    }

    private sealed class FakeUnboundSession : IProcessReadSession
    {
        public int Disposes { get; private set; }
        public Task<byte[]> ReadAsync(nuint address, int length, CancellationToken ct) =>
            Task.FromResult(new byte[length]);
        public void Dispose() => Disposes++;
    }

    private sealed class FakeBoundSession(ProcessHandleIdentity identity) : IProcessHandleBoundReadSession
    {
        public int Inspections { get; private set; }
        public int Disposes { get; private set; }
        public int Published { get; private set; }
        public int ExitOnInspection { get; init; }
        public int RecycleOnInspection { get; init; }
        public bool ExitDuringNextRead { get; set; }
        private bool _exited;

        public ProcessHandleIdentity GetLiveHandleIdentity()
        {
            if (Disposes != 0) throw new ObjectDisposedException(nameof(FakeBoundSession));
            Inspections++;
            if (_exited || Inspections == ExitOnInspection)
                throw new InvalidOperationException("Simulated process exit");
            if (Inspections == RecycleOnInspection)
                return identity with { StartTimeUtcTicks = identity.StartTimeUtcTicks + 1 };
            return identity;
        }
        public Task<byte[]> ReadAsync(nuint address, int length, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            GetLiveHandleIdentity();
            if (ExitDuringNextRead) _exited = true;
            GetLiveHandleIdentity();
            Published++;
            return Task.FromResult(new byte[length]);
        }
        public void Dispose() => Disposes++;
    }
}
