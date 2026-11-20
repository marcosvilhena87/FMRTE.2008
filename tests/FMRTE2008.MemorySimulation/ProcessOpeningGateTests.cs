using FMRTE2008.Application;

namespace FMRTE2008.MemorySimulation;

/// <summary>Offline F09 tests; no native processes, clocks, Sleep or actual handles.</summary>
internal static class ProcessOpeningGateTests
{
    internal static async Task RunAsync()
    {
        int checks = 0;
        void Check(bool ok, string why)
        {
            if (!ok) throw new Exception("F09: " + why);
            checks++;
        }

        var expected = new ExecutableIdentity(SupportedBuildCatalog.Known802Sha256,
            "8.0.2", 52395, "x86");
        var original = new ProcessOpeningEvidence(123, 638000000000000000,
            @"C:\Games\fm.exe", expected);
        // Path.GetFileName on Linux is OS-specific: simulated platform-independent
        // Windows basename is provided as a bare executable name.
        original = original with { ExecutablePath = "fm.exe" };

        async Task Scenario(string name, ProcessOpeningEvidence before,
            ProcessOpeningEvidence after, bool acquireExpected, Type errorType,
            Action? afterAcquire = null, bool cancelAfterAcquire = false)
        {
            var source = new CountingSession();
            int inspections = 0, acquisitions = 0;
            using var cts = new CancellationTokenSource();
            async Task<ProcessOpeningEvidence> Inspect(CancellationToken ct)
            {
                await Task.Yield();
                return ++inspections == 1 ? before : after;
            }
            async Task<IProcessReadSession> Acquire(CancellationToken ct)
            {
                await Task.Yield();
                acquisitions++;
                afterAcquire?.Invoke();
                if (cancelAfterAcquire) cts.Cancel();
                return source;
            }
            await Reject(() => VerifiedProcessOpeningGate.OpenAsync(
                123, expected, Inspect, Acquire, cts.Token), errorType);
            Check(acquisitions == (acquireExpected ? 1 : 0), name + ": acquisition count");
            Check(source.Disposes == (acquireExpected ? 1 : 0), name + ": resource disposal");
            Check(source.Reads == 0, name + ": unexpected read");
        }

        var good = new CountingSession();
        var open = await VerifiedProcessOpeningGate.OpenAsync(123, expected,
            _ => Task.FromResult(original),
            _ => Task.FromResult<IProcessReadSession>(good));
        Check(ReferenceEquals(open, good), "valid opener did not return acquired source");
        Check(good.Disposes == 0, "successful opener disposed live source");
        open.Dispose();
        Check(good.Disposes == 1, "successful resource not released");

        await Scenario("PID mismatch before acquisition",
            original with { ProcessId = 124 }, original, false, typeof(UnauthorizedAccessException));
        await Scenario("executable name mismatch",
            original with { ExecutablePath = "other.exe" }, original,
            false, typeof(UnauthorizedAccessException));
        await Scenario("fingerprint mismatch",
            original with { Executable = expected with { Sha256 = new string('0', 64) } },
            original, false, typeof(UnauthorizedAccessException));
        await Scenario("missing process creation identity",
            original with { StartTimeUtcTicks = 0 }, original,
            false, typeof(UnauthorizedAccessException));
        await Scenario("process replaced reusing PID",
            original, original with { StartTimeUtcTicks = original.StartTimeUtcTicks + 1 },
            true, typeof(InvalidOperationException));
        await Scenario("different process after acquisition",
            original, original with { ProcessId = 124 },
            true, typeof(UnauthorizedAccessException));
        await Scenario("binary changed after acquisition",
            original, original with { Executable = expected with { FilePrivatePart = 42 } },
            true, typeof(UnauthorizedAccessException));
        await Scenario("canceled after handle acquired",
            original, original, true, typeof(OperationCanceledException),
            cancelAfterAcquire: true);

        var broken = new CountingSession();
        int attempts = 0;
        await Reject(() => VerifiedProcessOpeningGate.OpenAsync(123, expected,
            _ =>
            {
                attempts++;
                if (attempts > 1) throw new IOException("Second inspection failed");
                return Task.FromResult(original);
            },
            _ => Task.FromResult<IProcessReadSession>(broken)), typeof(IOException));
        Check(broken.Disposes == 1, "post-acquisition inspection failure leaked source");

        int failingAcquisitions = 0;
        await Reject(() => VerifiedProcessOpeningGate.OpenAsync(123, expected,
            _ => Task.FromResult(original),
            _ =>
            {
                failingAcquisitions++;
                throw new IOException("Acquire failed before returning a resource");
            }), typeof(IOException));
        Check(failingAcquisitions == 1, "acquisition failure retried implicitly");

        await Reject(() => VerifiedProcessOpeningGate.OpenAsync(123, expected,
            _ => Task.FromResult(original),
            _ => Task.FromResult<IProcessReadSession>(null!)),
            typeof(InvalidOperationException));
        Check(failingAcquisitions == 1, "null acquisition modified earlier state");

        int deniedAcquires = 0;
        using (var preCanceled = new CancellationTokenSource())
        {
            preCanceled.Cancel();
            await Reject(() => VerifiedProcessOpeningGate.OpenAsync(123, expected,
                _ => Task.FromResult(original),
                _ =>
                {
                    deniedAcquires++;
                    return Task.FromResult<IProcessReadSession>(new CountingSession());
                }, preCanceled.Token), typeof(OperationCanceledException));
        }
        Check(deniedAcquires == 0, "pre-cancel reached acquire");

        // Cancel while the second inspection is in flight: the acquired handle
        // must remain owned by the gate and be closed exactly once.
        var enteredSecond = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseSecond = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var pendingSource = new CountingSession();
        using (var pendingCts = new CancellationTokenSource())
        {
            int inspectCount = 0;
            async Task<ProcessOpeningEvidence> DelayedInspect(CancellationToken ct)
            {
                if (++inspectCount == 1) return original;
                enteredSecond.TrySetResult(true);
                await releaseSecond.Task;
                return original;
            }
            var pending = VerifiedProcessOpeningGate.OpenAsync(123, expected,
                DelayedInspect,
                _ => Task.FromResult<IProcessReadSession>(pendingSource),
                pendingCts.Token);
            await enteredSecond.Task;
            Check(pendingSource.Disposes == 0,
                "source disposed before second inspection finished");
            pendingCts.Cancel();
            releaseSecond.TrySetResult(true);
            await Reject(() => pending, typeof(OperationCanceledException));
            Check(pendingSource.Disposes == 1,
                "cancel during second inspection did not release once");
        }

        // Two independent opens at the same PID must not share handle ownership.
        // The first instance changes while the other remains unchanged.
        var staleOpened = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var resumeStale = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var oldHandle = new CountingSession();
        var newHandle = new CountingSession();
        int staleInspections = 0;
        async Task<ProcessOpeningEvidence> StaleInspect(CancellationToken ct)
        {
            if (++staleInspections == 1) return original;
            staleOpened.TrySetResult(true);
            await resumeStale.Task;
            return original with { StartTimeUtcTicks = original.StartTimeUtcTicks + 1 };
        }
        var staleOpening = VerifiedProcessOpeningGate.OpenAsync(123, expected,
            StaleInspect, _ => Task.FromResult<IProcessReadSession>(oldHandle));
        await staleOpened.Task;
        using (var independent = await VerifiedProcessOpeningGate.OpenAsync(123, expected,
            _ => Task.FromResult(original),
            _ => Task.FromResult<IProcessReadSession>(newHandle)))
        {
            Check(oldHandle.Disposes == 0 && newHandle.Disposes == 0,
                "overlapping opens closed a handle prematurely");
            resumeStale.TrySetResult(true);
            await Reject(() => staleOpening, typeof(InvalidOperationException));
            Check(oldHandle.Disposes == 1 && newHandle.Disposes == 0,
                "failure of one opening affected another handle");
        }
        Check(newHandle.Disposes == 1,
            "independent successful opening did not dispose once");

        var fresh = new CountingSession();
        using (var retry = await VerifiedProcessOpeningGate.OpenAsync(123, expected,
            _ => Task.FromResult(original),
            _ => Task.FromResult<IProcessReadSession>(fresh)))
        {
            Check(fresh.Disposes == 0, "fresh session was prematurely disposed");
        }
        Check(fresh.Disposes == 1, "fresh session leaked");
        Console.WriteLine($"OK: {checks} F09 process identity/acquisition assertions (offline)");
    }

    private static async Task Reject(Func<Task> run, Type expected)
    {
        try { await run(); }
        catch (Exception ex) when (ex.GetType() == expected) { return; }
        throw new Exception("F09 expected " + expected.Name);
    }

    private sealed class CountingSession : IProcessReadSession
    {
        public int Disposes { get; private set; }
        public int Reads { get; private set; }
        public Task<byte[]> ReadAsync(nuint address, int length, CancellationToken ct)
        {
            Reads++;
            return Task.FromResult(new byte[length]);
        }
        public void Dispose() => Disposes++;
    }
}
