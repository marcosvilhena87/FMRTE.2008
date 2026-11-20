using System.Diagnostics;
using System.Threading;
using Microsoft.Win32.SafeHandles;

namespace FMRTE2008.WindowsResilience;

/// <summary>
/// F17: bounded Windows tests using only kernel events owned by this test process.
/// No process discovery, external handles, native reads, writes or fm.exe.
/// </summary>
internal static class F17ResourceConcurrencyTests
{
    private const int PartialCycles = 128;
    private const int ConcurrentCycles = 64;
    private static readonly TimeSpan Deadline = TimeSpan.FromSeconds(15);

    internal static async Task RunAsync()
    {
        if (!OperatingSystem.IsWindows())
            throw new PlatformNotSupportedException("F17 runs only on Windows.");

        NegativeLedgerControls();
        RunAcquisitionFaults();
        RunExceptionalCleanupModel();
        await RunControlledConcurrencyAsync();

        Console.WriteLine($"OK F17 Windows: {PartialCycles * 4} partial-acquisition scenarios, " +
            $"64 modeled exceptional cleanups, {ConcurrentCycles} coordinated lease/Dispose scenarios, " +
            "6 negative ledger controls and 1 exceptional independent-close control (self-owned kernel events only).");
    }

    private static void RunAcquisitionFaults()
    {
        var ledger = new Ledger();
        int interrupted = 0;
        for (int iteration = 0; iteration < PartialCycles; iteration++)
        {
            for (int stage = 0; stage < 4; stage++)
            {
                int firstId = iteration * 8 + stage * 2;
                int secondId = firstId + 1;
                ManualResetEvent? first = null;
                ManualResetEvent? second = null;
                try
                {
                    if (stage == 0) throw new IOException("before first acquisition");
                    first = new ManualResetEvent(false);
                    ledger.Acquire(firstId);
                    if (stage == 1) throw new IOException("after first acquisition");
                    second = new ManualResetEvent(false);
                    ledger.Acquire(secondId);
                    if (stage == 2) throw new IOException("after second acquisition");
                    if (!second.Set() || !second.WaitOne(0))
                        throw new Exception("F17 independent local event did not signal");
                }
                catch (IOException)
                {
                    interrupted++;
                }
                finally
                {
                    // Each close is independent, including when the other close throws.
                    try { if (first is not null) DisposeRecorded(first, firstId, ledger); }
                    finally { if (second is not null) DisposeRecorded(second, secondId, ledger); }
                }
                ledger.AssertBalanced();
            }
        }
        if (interrupted != PartialCycles * 3 || ledger.Acquisitions != PartialCycles * 5 ||
            ledger.DisposeAttempts != ledger.Acquisitions ||
            ledger.DisposeCompletions != ledger.Acquisitions)
            throw new Exception("F17 partial-acquisition totals did not reconcile");
    }

    private static void RunExceptionalCleanupModel()
    {
        // Injected cleanup callback failures are model-only: always actually dispose
        // the owned Windows event, and NEVER credit a failed callback as success.
        for (int i = 0; i < 64; i++)
        {
            var ledger = new Ledger();
            using var local = new ManualResetEvent(false);
            ledger.Acquire(i);
            try
            {
                ledger.AttemptDispose(i);
                throw new IOException("F17 simulated cleanup callback failed");
            }
            catch (IOException)
            {
                ledger.MarkUncertain(i);
            }
            finally
            {
                local.Dispose();
            }
            if (ledger.Acquisitions != 1 || ledger.DisposeAttempts != 1 ||
                ledger.DisposeCompletions != 0 || ledger.UncertainReleases != 1)
                throw new Exception("F17 uncertain release improperly counted as successful");
            // The fake callback does not operate on or poison the real kernel event.
            using var fresh = new ManualResetEvent(false);
            if (!fresh.Set() || !fresh.WaitOne(0))
                throw new Exception("F17 recovery after simulated cleanup fault failed");
        }
    }

    private static async Task RunControlledConcurrencyAsync()
    {
        var ledger = new Ledger();
        using var process = Process.GetCurrentProcess();
        int baseline = StableHandleCount(process);
        int maxDelta = int.MinValue;
        for (int i = 0; i < ConcurrentCycles; i++)
        {
            int firstId = 100000 + i * 2, secondId = firstId + 1;
            using var first = new ManualResetEvent(false);
            using var second = new ManualResetEvent(false);
            ledger.Acquire(firstId);
            ledger.Acquire(secondId);
            SafeWaitHandle safe = first.SafeWaitHandle;
            var entered = Signal();
            var release = Signal();

            // Worker has the lease before main thread closes the owner.
            Task pending = Task.Run(async () =>
            {
                bool added = false;
                try
                {
                    safe.DangerousAddRef(ref added);
                    if (!added || safe.IsInvalid)
                        throw new Exception("F17 worker lease acquisition failed");
                    ledger.LeaseAcquired();
                    entered.TrySetResult(true);
                    await release.Task.WaitAsync(Deadline);
                }
                finally
                {
                    if (added)
                    {
                        safe.DangerousRelease();
                        ledger.LeaseReleased();
                    }
                }
            });

            try
            {
                await entered.Task.WaitAsync(Deadline);
                DisposeRecorded(first, firstId, ledger);
                if (!second.Set() || !second.WaitOne(0))
                    throw new Exception("F17 independent event stopped progressing");
            }
            finally
            {
                release.TrySetResult(true);
                try
                {
                    await pending.WaitAsync(Deadline);
                }
                finally
                {
                    // This must run even if the worker failed or timed out.
                    try
                    {
                        if (!ledger.HasAttempt(secondId)) DisposeRecorded(second, secondId, ledger);
                    }
                    finally
                    {
                        // Never let cleanup of the independent event suppress a
                        // required attempt to clean up the original event.
                        if (!ledger.HasAttempt(firstId)) DisposeRecorded(first, firstId, ledger);
                    }
                }
            }
            if (!safe.IsClosed)
                throw new Exception("F17 SafeHandle remained open after release of final lease");
            ledger.AssertBalanced();
            if ((i + 1) % 16 == 0)
            {
                int delta = StableHandleCount(process) - baseline;
                maxDelta = Math.Max(maxDelta, delta);
                if (delta > 64) throw new Exception($"F17 excessive local handle growth: {delta}");
            }
        }
        if (ledger.Acquisitions != ConcurrentCycles * 2 ||
            ledger.DisposeCompletions != ConcurrentCycles * 2 ||
            ledger.LeasesTaken != ConcurrentCycles || ledger.LeasesReturned != ConcurrentCycles)
            throw new Exception("F17 coordinated concurrency totals mismatch");
        Console.WriteLine($"OK F17 coordinated concurrency: {ConcurrentCycles} cycles, " +
            $"{ledger.Acquisitions} local events, {ledger.LeasesReturned} released leases, " +
            $"maxHandleDelta={maxDelta} (tolerance=64).");
    }

    private static void NegativeLedgerControls()
    {
        ExpectFailure(() => { var l = new Ledger(); l.Acquire(1); l.Acquire(1); });
        ExpectFailure(() => { var l = new Ledger(); l.AttemptDispose(1); });
        ExpectFailure(() => { var l = new Ledger(); l.Acquire(1); l.AttemptDispose(1); l.AttemptDispose(1); });
        ExpectFailure(() => { var l = new Ledger(); l.Acquire(1); l.AttemptDispose(1); l.MarkUncertain(1); l.CompleteDispose(1); });
        ExpectFailure(() => { var l = new Ledger(); l.LeaseAcquired(); l.AssertBalanced(); });
        ExpectFailure(() => { var l = new Ledger(); l.Acquire(1); l.AttemptDispose(1); l.CompleteDispose(1); l.MarkUncertain(1); });
        ExceptionalIndependentCloseControl();
    }

    private static void ExceptionalIndependentCloseControl()
    {
        // Model an exceptional close callback while still closing both
        // self-owned Windows objects. The independent close must be attempted.
        var l = new Ledger();
        using var first = new ManualResetEvent(false);
        using var second = new ManualResetEvent(false);
        l.Acquire(800001);
        l.Acquire(800002);
        bool secondAttempted = false;
        try
        {
            try
            {
                l.AttemptDispose(800001);
                throw new IOException("F17 injected first close callback failure");
            }
            catch (IOException)
            {
                l.MarkUncertain(800001);
            }
            finally
            {
                try { first.Dispose(); }
                finally
                {
                    secondAttempted = true;
                    DisposeRecorded(second, 800002, l);
                }
            }
        }
        finally
        {
            // Using declarations also guarantee local event cleanup on failure.
        }
        if (!secondAttempted || l.DisposeAttempts != 2 ||
            l.DisposeCompletions != 1 || l.UncertainReleases != 1)
            throw new Exception("F17 exceptional cleanup suppressed independent close");
        using var recovered = new ManualResetEvent(false);
        if (!recovered.Set() || !recovered.WaitOne(0))
            throw new Exception("F17 independent recovery after failed callback");
    }

    private static void ExpectFailure(Action action)
    {
        try { action(); }
        catch (InvalidOperationException) { return; }
        throw new Exception("F17 negative ledger control escaped detection");
    }

    private static void DisposeRecorded(ManualResetEvent evt, int id, Ledger ledger)
    {
        ledger.AttemptDispose(id);
        evt.Dispose();
        ledger.CompleteDispose(id);
    }
    private static TaskCompletionSource<bool> Signal() =>
        new(TaskCreationOptions.RunContinuationsAsynchronously);
    private static int StableHandleCount(Process process)
    {
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        process.Refresh();
        return process.HandleCount;
    }

    private sealed class Ledger
    {
        private readonly object _gate = new();
        private readonly HashSet<int> _ever = new();
        private readonly HashSet<int> _active = new();
        private readonly HashSet<int> _attempted = new();
        private readonly HashSet<int> _uncertain = new();
        private int _successes, _leasesTaken, _leasesReturned;
        public int Acquisitions { get { lock (_gate) return _ever.Count; } }
        public int DisposeAttempts { get { lock (_gate) return _attempted.Count; } }
        public int DisposeCompletions { get { lock (_gate) return _successes; } }
        public int UncertainReleases { get { lock (_gate) return _uncertain.Count; } }
        public int LeasesTaken { get { lock (_gate) return _leasesTaken; } }
        public int LeasesReturned { get { lock (_gate) return _leasesReturned; } }
        public bool HasAttempt(int id) { lock (_gate) return _attempted.Contains(id); }
        public void Acquire(int id)
        {
            lock (_gate)
                if (!_ever.Add(id) || !_active.Add(id))
                    throw new InvalidOperationException("F17 duplicate acquisition");
        }
        public void AttemptDispose(int id)
        {
            lock (_gate)
                if (!_active.Contains(id) || !_attempted.Add(id))
                    throw new InvalidOperationException("F17 duplicate or unknown close attempt");
        }
        public void CompleteDispose(int id)
        {
            lock (_gate)
            {
                if (!_attempted.Contains(id) || _uncertain.Contains(id) || !_active.Remove(id))
                    throw new InvalidOperationException("F17 unverified close completion");
                _successes++;
            }
        }
        public void MarkUncertain(int id)
        {
            lock (_gate)
                if (!_attempted.Contains(id) || !_active.Contains(id) || !_uncertain.Add(id))
                    throw new InvalidOperationException("F17 invalid uncertain release");
        }
        public void LeaseAcquired() { lock (_gate) _leasesTaken++; }
        public void LeaseReleased() { lock (_gate) _leasesReturned++; }
        public void AssertBalanced()
        {
            lock (_gate)
                if (_active.Count != 0 || _attempted.Count != _successes ||
                    _uncertain.Count != 0 || _leasesTaken != _leasesReturned)
                    throw new InvalidOperationException("F17 leaked resource or lease");
        }
    }
}
