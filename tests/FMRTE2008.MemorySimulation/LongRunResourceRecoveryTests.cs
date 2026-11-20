using System.ComponentModel;
using FMRTE2008.Application;

namespace FMRTE2008.MemorySimulation;

/// <summary>
/// F15: bounded, reproducible long-run recovery. The ledger is independent
/// of NativeReadOnlyGuard state and accounts for every simulated acquisition
/// and close attempt. No OS handles, process access, sleep, or random scheduling.
/// </summary>
internal static class LongRunResourceRecoveryTests
{
    private const int Cycles = 10000;
    private const int HandlesPerCycle = 3;

    private sealed class ResourceLedger
    {
        private readonly HashSet<int> _live = new();
        private readonly HashSet<int> _ever = new();
        private int _closes;
        private int _peak;
        public int Acquired => _ever.Count;
        public int Closes => _closes;
        public int Active => _live.Count;
        public int Peak => _peak;

        public void Acquire(int id)
        {
            if (!_ever.Add(id) || !_live.Add(id))
                throw new Exception($"F15 duplicate resource acquisition: {id}");
            _peak = Math.Max(_peak, _live.Count);
        }

        public void Close(int id)
        {
            _closes++;
            if (!_live.Remove(id))
                throw new Exception($"F15 double-close or close without acquisition: {id}");
        }
    }

    internal static async Task RunAsync()
    {
        var ledger = new ResourceLedger();
        int verified = 0, failedSessions = 0, recovered = 0, reads = 0, inspections = 0;
        for (int cycle = 0; cycle < Cycles; cycle++)
        {
            // Two independent poisoned sessions, then a new valid session.
            // Each failure changes in a deterministic 5-way round robin.
            for (int stage = 0; stage < HandlesPerCycle; stage++)
            {
                int id = cycle * HandlesPerCycle + stage + 1;
                var identity = new ProcessHandleIdentity(16000 + cycle,
                    638600000000000000 + id);
                bool recover = stage == 2;
                int mode = (cycle + stage) % 5;
                int localInspections = 0, localNativeCalls = 0, localCloseAttempts = 0;
                var entered = NewSignal();
                var release = NewSignal();
                ledger.Acquire(id);
                using var cts = new CancellationTokenSource();
                var session = new NativeReadOnlyGuard(
                    () =>
                    {
                        localInspections++;
                        inspections++;
                        if (!recover && ((mode == 2 && localInspections == 1) ||
                                         (mode == 3 && localInspections == 2)))
                            throw new IOException("F15 injected identity fault");
                        return identity;
                    },
                    async (_, bytes, _) =>
                    {
                        localNativeCalls++;
                        reads++;
                        if (!recover && mode == 4)
                        {
                            entered.TrySetResult(true);
                            await release.Task;
                        }
                        bytes[0] = 0x63;
                        if (!recover && mode == 0)
                            return new NativeReadOutcome(false, (nuint)bytes.Length, 5);
                        if (!recover && mode == 1)
                            return new NativeReadOutcome(true, (nuint)(bytes.Length - 1), 0);
                        return new NativeReadOutcome(true, (nuint)bytes.Length, 0);
                    },
                    () =>
                    {
                        localCloseAttempts++;
                        ledger.Close(id);
                    });

                int beforeRead = reads, beforeInspection = inspections;
                if (recover)
                {
                    byte[] data = await session.ReadAsync((nuint)0x1000, 8, default);
                    Check(data.Length == 8 && data[0] == 0x63,
                        cycle, stage, "new session failed to publish valid bytes");
                    Check(localNativeCalls == 1 && localInspections == 2,
                        cycle, stage, "healthy recovery has unexpected calls");
                    recovered++;
                }
                else
                {
                    if (mode == 4)
                    {
                        Task<byte[]> pending = session.ReadAsync((nuint)0x1000, 8, cts.Token);
                        await entered.Task;
                        cts.Cancel();
                        Check(localCloseAttempts == 0, cycle, stage,
                            "pending read closed before completion");
                        release.TrySetResult(true);
                        await Reject<OperationCanceledException>(() => pending);
                    }
                    else if (mode == 0)
                        await Reject<Win32Exception>(() =>
                            session.ReadAsync((nuint)0x1000, 8, default));
                    else if (mode == 1)
                        await Reject<InvalidDataException>(() =>
                            session.ReadAsync((nuint)0x1000, 8, default));
                    else
                        await Reject<IOException>(() =>
                            session.ReadAsync((nuint)0x1000, 8, default));

                    Check(localNativeCalls == (mode == 2 ? 0 : 1),
                        cycle, stage, "native invocation count after failure");
                    Check(localInspections == (mode == 3 ? 2 : 1),
                        cycle, stage, "identity inspection count after failure");
                    Check(localCloseAttempts == 1, cycle, stage,
                        "poisoned session did not close exactly once");
                    failedSessions++;
                }

                session.Dispose();
                session.Dispose();
                Check(localCloseAttempts == 1, cycle, stage, "double close after Dispose");
                int nativeBefore = reads, inspectBefore = inspections;
                await Reject<ObjectDisposedException>(() =>
                    session.ReadAsync((nuint)0x2000, 8, default));
                RejectSync<ObjectDisposedException>(() => session.GetLiveHandleIdentity());
                Check(reads == nativeBefore && inspections == inspectBefore,
                    cycle, stage, "terminal session reached simulated provider");
                Check(ledger.Active == 0 && ledger.Closes == ledger.Acquired,
                    cycle, stage, "resource ledger leak after recovery stage");
                Check(reads >= beforeRead && inspections >= beforeInspection,
                    cycle, stage, "monotonic provider counters changed backwards");
                verified += recover ? 6 : (mode == 4 ? 8 : 7);
            }
        }

        if (ledger.Acquired != Cycles * HandlesPerCycle ||
            ledger.Closes != ledger.Acquired ||
            ledger.Active != 0 || ledger.Peak != 1 ||
            failedSessions != Cycles * 2 || recovered != Cycles)
            throw new Exception("F15 final resource accounting or recovery total mismatch");

        Console.WriteLine($"OK: {Cycles} F15 long-run recovery cycles, " +
            $"{ledger.Acquired} independently accounted simulated handles, " +
            $"{failedSessions} failed and {recovered} recovered sessions, " +
            $"{verified} lifetime assertions (offline)");
        await OverlappingLifetimeAsync();
    }

    private static async Task OverlappingLifetimeAsync()
    {
        const int Iterations = 128;
        var ledger = new ResourceLedger();
        int assertions = 0;
        for (int iteration = 0; iteration < Iterations; iteration++)
        {
            // Explicitly create two simultaneous resource leases, and dispose
            // one while native reading remains pending. The second resource
            // must succeed without releasing the first prematurely.
            int first = 100000 + iteration * 2;
            int second = first + 1;
            ledger.Acquire(first);
            ledger.Acquire(second);
            var entered = NewSignal();
            var release = NewSignal();
            int nativeFirst = 0, nativeSecond = 0;
            var id = new ProcessHandleIdentity(25000 + iteration,
                638700000000000000 + iteration);
            var firstSession = new NativeReadOnlyGuard(
                () => id,
                async (_, bytes, _) =>
                {
                    nativeFirst++;
                    entered.TrySetResult(true);
                    await release.Task;
                    bytes[0] = 0x41;
                    return new NativeReadOutcome(true, (nuint)bytes.Length, 0);
                },
                () => ledger.Close(first));
            var secondSession = new NativeReadOnlyGuard(
                () => id,
                (_, bytes, _) =>
                {
                    nativeSecond++;
                    bytes[0] = 0x42;
                    return Task.FromResult(new NativeReadOutcome(true, (nuint)bytes.Length, 0));
                },
                () => ledger.Close(second));

            Task<byte[]> pending = firstSession.ReadAsync((nuint)0x1000, 8, default);
            await entered.Task;
            firstSession.Dispose();
            if (ledger.Active != 2 || ledger.Closes != iteration * 2)
                throw new Exception("F15 pending handle closed prematurely");
            assertions++;
            byte[] independent = await secondSession.ReadAsync((nuint)0x2000, 8, default);
            if (independent[0] != 0x42 || nativeSecond != 1 ||
                ledger.Active != 2)
                throw new Exception("F15 independent resource could not progress");
            assertions++;
            release.TrySetResult(true);
            await Reject<ObjectDisposedException>(() => pending);
            secondSession.Dispose();
            firstSession.Dispose();
            secondSession.Dispose();
            if (ledger.Active != 0 || ledger.Closes != (iteration + 1) * 2 ||
                nativeFirst != 1)
                throw new Exception("F15 overlapping resource accounting failure");
            assertions++;
        }

        Console.WriteLine($"OK: {Iterations} F15 overlapping leased-session scenarios, " +
            $"{ledger.Acquired} simulated resources, {assertions} assertions (offline)");
    }

    private static TaskCompletionSource<bool> NewSignal() =>
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    private static void Check(bool condition, int cycle, int stage, string message)
    {
        if (!condition) throw new Exception(
            $"F15 cycle={cycle} stage={stage}: {message}");
    }

    private static void RejectSync<T>(Action action) where T : Exception
    {
        try { action(); }
        catch (Exception ex) when (ex.GetType() == typeof(T)) { return; }
        throw new Exception($"F15 expected {typeof(T).Name}");
    }

    private static async Task Reject<T>(Func<Task> action) where T : Exception
    {
        try { await action(); }
        catch (Exception ex) when (ex.GetType() == typeof(T)) { return; }
        throw new Exception($"F15 expected {typeof(T).Name}");
    }
}
