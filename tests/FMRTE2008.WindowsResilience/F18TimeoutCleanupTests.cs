using System.Diagnostics;
using Microsoft.Win32.SafeHandles;

namespace FMRTE2008.WindowsResilience;

/// <summary>F18: deterministic schedules with self-owned Windows events only.
/// No external process handle, FM2008 integration, native read/write or operational map.</summary>
internal static class F18TimeoutCleanupTests
{
    private const int Cycles = 64;
    private static readonly TimeSpan Deadline = TimeSpan.FromSeconds(15);
    private static TaskCompletionSource<bool> Signal() =>
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    internal static async Task RunAsync()
    {
        if (!OperatingSystem.IsWindows())
            throw new PlatformNotSupportedException("F18 requires Windows.");
        NegativeOracleControls();
        PartialAcquisitionAndRecovery();
        await TimeoutAndConcurrentDisposeAsync();
        Console.WriteLine($"OK F18 Windows: {Cycles} timeouts with observed teardown, " +
            $"{Cycles * 4} partial-acquisition stages, 2 consecutive modeled faults per cycle, " +
            "7 negative oracle controls; self-owned events only.");
    }

    private enum ReleaseState { Created, DisposeAttempted, DisposeCompleted, ReleaseUncertain }
    private sealed class Oracle
    {
        private readonly object _gate = new();
        private readonly Dictionary<int, ReleaseState> _states = new();
        private readonly HashSet<int> _leases = new();
        private readonly HashSet<int> _pending = new();
        public int Created { get { lock (_gate) return _states.Count; } }
        public int Completed { get { lock (_gate) return _states.Values.Count(s => s == ReleaseState.DisposeCompleted); } }
        public int Uncertain { get { lock (_gate) return _states.Values.Count(s => s == ReleaseState.ReleaseUncertain); } }
        public void Create(int id) { lock (_gate) { if (!_states.TryAdd(id, ReleaseState.Created)) Fail("duplicate create"); } }
        public void BeginClose(int id)
        {
            lock (_gate)
            {
                if (!_states.TryGetValue(id, out var state) || state != ReleaseState.Created) Fail("invalid close attempt");
                _states[id] = ReleaseState.DisposeAttempted;
            }
        }
        public void CompleteClose(int id)
        {
            lock (_gate)
            {
                if (!_states.TryGetValue(id, out var state) || state != ReleaseState.DisposeAttempted) Fail("invalid close completion");
                _states[id] = ReleaseState.DisposeCompleted;
            }
        }
        public void UncertainClose(int id)
        {
            lock (_gate)
            {
                if (!_states.TryGetValue(id, out var state) || state != ReleaseState.DisposeAttempted) Fail("invalid uncertain close");
                _states[id] = ReleaseState.ReleaseUncertain;
            }
        }
        public void Lease(int id) { lock (_gate) { if (!_states.ContainsKey(id) || !_leases.Add(id)) Fail("invalid lease"); } }
        public void ReleaseLease(int id) { lock (_gate) { if (!_leases.Remove(id)) Fail("unmatched lease"); } }
        public void Started(int id) { lock (_gate) { if (!_pending.Add(id)) Fail("duplicate task"); } }
        public void Finished(int id) { lock (_gate) { if (!_pending.Remove(id)) Fail("unmatched task completion"); } }
        public void AssertSettled(int expectedUncertain = 0)
        {
            lock (_gate)
            {
                if (_leases.Count != 0 || _pending.Count != 0 || _states.Values.Any(s =>
                        s is ReleaseState.Created or ReleaseState.DisposeAttempted) ||
                    _states.Values.Count(s => s == ReleaseState.ReleaseUncertain) != expectedUncertain)
                    Fail("resource, lease, task or uncertainty not reconciled");
            }
        }
        private static void Fail(string message) => throw new InvalidOperationException("F18 oracle: " + message);
    }

    private static void Close(ManualResetEvent value, int id, Oracle oracle)
    {
        oracle.BeginClose(id);
        value.Dispose();
        oracle.CompleteClose(id); // Never credit before Dispose returns.
    }
    private static void MustReject(Action action)
    {
        try { action(); }
        catch (InvalidOperationException) { return; }
        throw new Exception("F18 negative oracle control passed incorrectly");
    }

    private static void NegativeOracleControls()
    {
        MustReject(() => { var o = new Oracle(); o.Create(1); o.Create(1); });
        MustReject(() => new Oracle().BeginClose(1));
        MustReject(() => { var o = new Oracle(); o.Create(1); o.BeginClose(1); o.BeginClose(1); });
        MustReject(() => { var o = new Oracle(); o.Create(1); o.BeginClose(1); o.CompleteClose(1); o.UncertainClose(1); });
        MustReject(() => { var o = new Oracle(); o.Create(1); o.Lease(1); o.AssertSettled(); });
        MustReject(() => { var o = new Oracle(); o.Started(1); o.AssertSettled(); });
        MustReject(() => { var o = new Oracle(); o.Create(1); o.BeginClose(1); o.UncertainClose(1); o.AssertSettled(); });
        // Negative omission of a barrier is detected while the task really remains pending.
        var entered = Signal();
        if (entered.Task.IsCompleted)
            throw new Exception("F18 omitted-barrier negative control failed");
    }

    private static void PartialAcquisitionAndRecovery()
    {
        for (int cycle = 0; cycle < Cycles; cycle++)
        {
            for (int stage = 0; stage < 4; stage++)
            {
                var oracle = new Oracle();
                ManualResetEvent? first = null, second = null;
                Exception? primary = null, cleanup = null;
                try
                {
                    if (stage == 0) throw new IOException("injected before acquisition");
                    first = new ManualResetEvent(false);
                    oracle.Create(1);
                    if (stage == 1) throw new IOException("injected after first acquisition");
                    second = new ManualResetEvent(false);
                    oracle.Create(2);
                    if (stage == 2) throw new IOException("injected after second acquisition");
                    // Local callback fault, not an actual failure of Windows CloseHandle.
                    throw new IOException("injected primary operation failure");
                }
                catch (IOException ex) { primary = ex; }
                finally
                {
                    try
                    {
                        if (first is not null)
                        {
                            if (stage == 2)
                            {
                                oracle.BeginClose(1);
                                try { throw new IOException("injected cleanup callback fault"); }
                                catch (IOException ex) { cleanup = ex; oracle.UncertainClose(1); }
                                finally { first.Dispose(); } // Real object always cleaned up.
                            }
                            else Close(first, 1, oracle);
                        }
                    }
                    finally { if (second is not null) Close(second, 2, oracle); }
                }
                if (primary is null || (cleanup is null) != (stage != 2))
                    throw new Exception("F18 primary/cleanup diagnostic priority lost");
                oracle.AssertSettled(stage == 2 ? 1 : 0);
                if (oracle.Completed + oracle.Uncertain != oracle.Created)
                    throw new Exception("F18 independent acquisition accounting mismatch");
                using var recovered = new ManualResetEvent(false);
                if (!recovered.Set() || !recovered.WaitOne(0))
                    throw new Exception("F18 recovery after faults failed");
            }
        }
    }

    private static async Task TimeoutAndConcurrentDisposeAsync()
    {
        using var process = Process.GetCurrentProcess();
        int baseline = Handles(process);
        int maxDelta = int.MinValue;
        for (int cycle = 0; cycle < Cycles; cycle++)
        {
            var oracle = new Oracle();
            int firstId = cycle * 2 + 1000, otherId = firstId + 1;
            using var owned = new ManualResetEvent(false);
            using var other = new ManualResetEvent(false);
            oracle.Create(firstId);
            oracle.Create(otherId);
            SafeWaitHandle safe = owned.SafeWaitHandle;
            var entered = Signal();
            var release = Signal();
            var taskId = cycle + 1;
            oracle.Started(taskId);
            Task worker = Task.Run(async () =>
            {
                bool added = false;
                try
                {
                    safe.DangerousAddRef(ref added);
                    if (!added || safe.IsInvalid) throw new Exception("F18 lease acquisition failed");
                    oracle.Lease(firstId);
                    entered.TrySetResult(true);
                    await release.Task; // WaitAsync timeout must NOT cancel this worker.
                    if (cycle % 2 == 0) throw new IOException("injected worker failure after release");
                }
                finally
                {
                    try
                    {
                        if (added)
                        {
                            // Real release is never fault-injected; model errors separately.
                            safe.DangerousRelease();
                            oracle.ReleaseLease(firstId);
                        }
                    }
                    finally { oracle.Finished(taskId); }
                }
            });
            Exception? primary = null;
            try
            {
                await entered.Task.WaitAsync(Deadline);
                try
                {
                    await worker.WaitAsync(TimeSpan.Zero);
                    throw new Exception("F18 expected observer timeout");
                }
                catch (TimeoutException ex) { primary = ex; }
                if (worker.IsCompleted || primary is null)
                    throw new Exception("F18 WaitAsync unexpectedly terminated worker");
                Close(owned, firstId, oracle); // Owner closed while lease is active.
                if (!other.Set() || !other.WaitOne(0))
                    throw new Exception("F18 independent local event stopped progressing");
            }
            finally
            {
                release.TrySetResult(true); // Always unblocks worker, even on failures.
                try
                {
                    try { await worker.WaitAsync(Deadline); }
                    catch (IOException) when (cycle % 2 == 0) { /* Expected and observed. */ }
                }
                finally
                {
                    // Independent teardown must run even on a fault or timeout.
                    Close(other, otherId, oracle);
                    if (oracle.Completed == 1) Close(owned, firstId, oracle);
                }
            }
            if (!worker.IsCompleted || !safe.IsClosed)
                throw new Exception("F18 task/lease unfinished after teardown");
            oracle.AssertSettled();
            if (oracle.Created != 2 || oracle.Completed != 2 || oracle.Uncertain != 0)
                throw new Exception("F18 independent ledger mismatch");
            if ((cycle + 1) % 16 == 0)
            {
                int delta = Handles(process) - baseline;
                maxDelta = Math.Max(maxDelta, delta);
                if (delta > 64) throw new Exception($"F18 handle budget exceeded: {delta}");
            }
        }
        Console.WriteLine($"OK F18 resource budget: baseline={baseline}, maxHandleDelta={maxDelta}, tolerance=64.");
    }

    private static int Handles(Process process)
    {
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        process.Refresh();
        return process.HandleCount;
    }
}
