using System.Diagnostics;
using Microsoft.Win32.SafeHandles;

namespace FMRTE2008.WindowsResilience;

/// <summary>
/// F18: controlled Windows event schedules. All native resources belong to this test process.
/// Never opens processes, accesses fm.exe, loads an approved map or writes game memory.
/// </summary>
internal static class F18TimeoutCleanupTests
{
    private const int Cycles = 64;
    private static readonly TimeSpan Deadline = TimeSpan.FromSeconds(15);
    private static TaskCompletionSource<bool> Signal() =>
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    private enum State { Created, DisposeAttempted, DisposeCompleted, ReleaseUncertain }

    private sealed class Oracle
    {
        private readonly object _gate = new();
        private readonly Dictionary<int, State> _resources = new();
        private readonly HashSet<int> _leases = new();
        private readonly HashSet<int> _tasks = new();
        public int Created { get { lock (_gate) return _resources.Count; } }
        public int Completed { get { lock (_gate) return _resources.Values.Count(s => s == State.DisposeCompleted); } }
        public int Uncertain { get { lock (_gate) return _resources.Values.Count(s => s == State.ReleaseUncertain); } }
        public bool NeedsClose(int id)
        {
            lock (_gate) return _resources.TryGetValue(id, out State state) && state == State.Created;
        }
        public void Create(int id)
        {
            lock (_gate) if (!_resources.TryAdd(id, State.Created)) Fail("duplicate resource");
        }
        public void BeginClose(int id)
        {
            lock (_gate)
            {
                if (!_resources.TryGetValue(id, out var state) || state != State.Created)
                    Fail("invalid close attempt");
                _resources[id] = State.DisposeAttempted;
            }
        }
        public void CompleteClose(int id)
        {
            lock (_gate)
            {
                if (!_resources.TryGetValue(id, out var state) || state != State.DisposeAttempted)
                    Fail("invalid completion");
                _resources[id] = State.DisposeCompleted;
            }
        }
        public void MarkUncertain(int id)
        {
            lock (_gate)
            {
                if (!_resources.TryGetValue(id, out var state) || state != State.DisposeAttempted)
                    Fail("invalid uncertain transition");
                _resources[id] = State.ReleaseUncertain;
            }
        }
        public void Lease(int id)
        {
            lock (_gate)
            {
                if (!_resources.TryGetValue(id, out var state) || state != State.Created ||
                    !_leases.Add(id)) Fail("lease on missing, closed, or already leased resource");
            }
        }
        // The lease may outlive an owner Dispose, but cannot be returned twice.
        public void ReturnLease(int id)
        {
            lock (_gate) if (!_leases.Remove(id)) Fail("invalid lease return");
        }
        public void Started(int id)
        {
            lock (_gate) if (!_tasks.Add(id)) Fail("duplicate task");
        }
        public void Finished(int id)
        {
            lock (_gate) if (!_tasks.Remove(id)) Fail("unknown task finished");
        }
        public void AssertSettled(int expectedUncertain = 0)
        {
            lock (_gate)
            {
                if (_leases.Count != 0 || _tasks.Count != 0 ||
                    _resources.Values.Any(s => s is State.Created or State.DisposeAttempted) ||
                    _resources.Values.Count(s => s == State.ReleaseUncertain) != expectedUncertain)
                    Fail("unsettled resources, tasks, leases, or uncertainty");
            }
        }
        private static void Fail(string message) =>
            throw new InvalidOperationException("F18 oracle: " + message);
    }

    private sealed class Faults
    {
        public Exception? Primary { get; private set; }
        public List<Exception> Cleanup { get; } = new();
        public void SetPrimary(Exception error)
        {
            if (Primary is not null) throw new InvalidOperationException("F18 duplicate primary");
            Primary = error;
        }
        public void RecordCleanup(Exception error) => Cleanup.Add(error);
        public void TryCleanup(Action action)
        {
            try { action(); }
            catch (Exception ex) { RecordCleanup(ex); }
        }
        public async Task TryCleanupAsync(Func<Task> action)
        {
            try { await action(); }
            catch (Exception ex) { RecordCleanup(ex); }
        }
        public Exception Combined(Exception? terminal = null)
        {
            var all = new List<Exception>();
            if (Primary is not null) all.Add(Primary);
            all.AddRange(Cleanup);
            if (terminal is not null) all.Add(terminal);
            if (all.Count == 0) throw new InvalidOperationException("F18 no errors to combine");
            return all.Count == 1 ? all[0] :
                new AggregateException("F18 primary, cleanup and teardown errors retained in order", all);
        }
        public void ThrowIfAny()
        {
            if (Primary is null && Cleanup.Count == 0) return;
            System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(Combined()).Throw();
        }
    }

    private static void Close(ManualResetEvent evt, int id, Oracle oracle)
    {
        oracle.BeginClose(id);
        try
        {
            evt.Dispose();
            oracle.CompleteClose(id); // Credit ONLY successful return.
        }
        catch
        {
            oracle.MarkUncertain(id);
            throw;
        }
    }

    private static void MustReject(Action action)
    {
        try { action(); }
        catch (InvalidOperationException) { return; }
        throw new Exception("F18 negative oracle control incorrectly accepted invalid history");
    }

    internal static async Task RunAsync()
    {
        if (!OperatingSystem.IsWindows())
            throw new PlatformNotSupportedException("F18 requires Windows.");
        NegativeOracleControls();
        await PendingTaskAndCancellationControlsAsync();
        FaultPriorityAndIndependentTeardownControls();
        ModeledReleaseFailureControls();
        PartialAcquisitionAndRecovery();
        ConsecutiveFaultRecovery();
        await TimeoutAndConcurrentDisposeAsync();
        Console.WriteLine($"OK F18 Windows: {Cycles} observed timeouts, {Cycles * 4} acquisition stages, " +
            $"{Cycles} two-fault recovery cycles, 10 negative oracle controls, " +
            "cancellation/exception, modeled release failures, primary+cleanup and independent teardown controls.");
    }

    private static void NegativeOracleControls()
    {
        MustReject(() => { var o = new Oracle(); o.Create(1); o.Create(1); });
        MustReject(() => new Oracle().BeginClose(1));
        MustReject(() => { var o = new Oracle(); o.Create(1); o.BeginClose(1); o.BeginClose(1); });
        MustReject(() => { var o = new Oracle(); o.Create(1); o.BeginClose(1); o.CompleteClose(1); o.MarkUncertain(1); });
        MustReject(() => { var o = new Oracle(); o.Create(1); o.Lease(1); o.AssertSettled(); });
        MustReject(() => { var o = new Oracle(); o.Started(1); o.AssertSettled(); });
        MustReject(() => { var o = new Oracle(); o.Create(1); o.BeginClose(1); o.MarkUncertain(1); o.AssertSettled(); });
        MustReject(() => { var o = new Oracle(); o.Create(1); o.BeginClose(1); o.CompleteClose(1); o.Lease(1); });
        MustReject(() => { var o = new Oracle(); o.Create(1); o.BeginClose(1); o.MarkUncertain(1); o.Lease(1); });
        MustReject(() => { var o = new Oracle(); o.Create(1); o.Lease(1); o.ReturnLease(1); o.ReturnLease(1); });
    }

    private static async Task PendingTaskAndCancellationControlsAsync()
    {
        var oracle = new Oracle();
        var entered = Signal();
        var release = Signal();
        oracle.Started(91);
        Task pending = Task.Run(async () =>
        {
            entered.TrySetResult(true);
            try { await release.Task; }
            finally { oracle.Finished(91); }
        });
        try
        {
            await entered.Task.WaitAsync(Deadline);
            MustReject(() => oracle.AssertSettled());
            if (pending.IsCompleted) throw new Exception("F18 pending-task control did not block");
            try
            {
                await pending.WaitAsync(TimeSpan.Zero);
                throw new Exception("F18 missing observer timeout");
            }
            catch (TimeoutException) { }
            if (pending.IsCompleted) throw new Exception("F18 observer timeout cancelled the worker");
        }
        finally
        {
            release.TrySetResult(true);
            await pending.WaitAsync(Deadline);
        }
        if (!pending.IsCompletedSuccessfully) throw new Exception("F18 pending task not actually finished");
        oracle.AssertSettled();

        using var cts = new CancellationTokenSource();
        var afterCancel = Signal();
        var cancelledEntered = Signal();
        Task faulted = Task.Run(async () =>
        {
            cancelledEntered.TrySetResult(true);
            await afterCancel.Task;
            if (!cts.IsCancellationRequested) throw new Exception("F18 missing cancellation");
            throw new IOException("F18 injected fault after cancellation");
        });
        try
        {
            await cancelledEntered.Task.WaitAsync(Deadline);
            cts.Cancel();
        }
        finally { afterCancel.TrySetResult(true); }
        try
        {
            await faulted.WaitAsync(Deadline);
            throw new Exception("F18 missing post-cancellation worker fault");
        }
        catch (IOException ex) when (ex.Message.Contains("after cancellation", StringComparison.Ordinal)) { }
        if (!faulted.IsCompleted || !faulted.IsFaulted)
            throw new Exception("F18 post-cancellation exception was not observed");
    }

    private static void FaultPriorityAndIndependentTeardownControls()
    {
        var faults = new Faults();
        var primary = new IOException("primary");
        var firstCleanup = new InvalidOperationException("first cleanup");
        faults.SetPrimary(primary);
        int attempts = 0;
        faults.TryCleanup(() => { attempts++; throw firstCleanup; });
        faults.TryCleanup(() => attempts++);
        if (attempts != 2 || !ReferenceEquals(faults.Primary, primary) ||
            faults.Cleanup.Count != 1 || !ReferenceEquals(faults.Cleanup[0], firstCleanup))
            throw new Exception("F18 independent teardown or diagnostics failed");
        var terminal = new TimeoutException("modeled failed join");
        if (faults.Combined(terminal) is not AggregateException combined ||
            combined.InnerExceptions.Count != 3 ||
            !ReferenceEquals(combined.InnerExceptions[0], primary) ||
            !ReferenceEquals(combined.InnerExceptions[1], firstCleanup) ||
            !ReferenceEquals(combined.InnerExceptions[2], terminal))
            throw new Exception("F18 join timeout masked original exception");
        try
        {
            faults.ThrowIfAny();
            throw new Exception("F18 lost aggregated diagnostics");
        }
        catch (AggregateException ex) when (ex.InnerExceptions.Count == 2 &&
            ReferenceEquals(ex.InnerExceptions[0], primary) &&
            ReferenceEquals(ex.InnerExceptions[1], firstCleanup)) { }

        // Both real, self-owned Windows events MUST be closed even when a
        // model callback before the first close fails.
        using var first = new ManualResetEvent(false);
        using var second = new ManualResetEvent(false);
        var oracle = new Oracle();
        oracle.Create(1); oracle.Create(2);
        var diagnostics = new Faults();
        diagnostics.TryCleanup(() =>
        {
            try { throw new IOException("modeled cleanup callback fault"); }
            finally { Close(first, 1, oracle); }
        });
        diagnostics.TryCleanup(() => Close(second, 2, oracle));
        if (diagnostics.Cleanup.Count != 1 || oracle.Completed != 2 ||
            !first.SafeWaitHandle.IsClosed || !second.SafeWaitHandle.IsClosed)
            throw new Exception("F18 independent native closure was skipped");
        oracle.AssertSettled();
    }

    private static void ModeledReleaseFailureControls()
    {
        // A managed callback can fail before native release. The oracle MUST
        // reject a settled history while the real SafeHandle lease is live.
        // Never inject an actual OS/DangerousRelease failure.
        for (int cycle = 0; cycle < Cycles; cycle++)
        {
            using var owned = new ManualResetEvent(false);
            var oracle = new Oracle();
            oracle.Create(cycle);
            SafeWaitHandle safe = owned.SafeWaitHandle;
            bool added = false;
            safe.DangerousAddRef(ref added);
            if (!added) throw new Exception("F18 fixture could not acquire lease");
            oracle.Lease(cycle);
            Close(owned, cycle, oracle);
            var errors = new Faults();
            var primary = new IOException("modeled worker failure");
            errors.SetPrimary(primary);
            try
            {
                throw new InvalidOperationException("injected managed release callback fault");
            }
            catch (InvalidOperationException ex)
            {
                errors.RecordCleanup(ex);
                MustReject(() => oracle.AssertSettled());
            }
            finally
            {
                // Actual native release must be attempted even after modeled failure.
                // Crucially, no ledger success is recorded if DangerousRelease throws.
                safe.DangerousRelease();
                oracle.ReturnLease(cycle);
            }
            if (!safe.IsClosed) throw new Exception("F18 modeled release left native handle open");
            oracle.AssertSettled();
            if (errors.Combined() is not AggregateException aggregate ||
                aggregate.InnerExceptions.Count != 2 ||
                !ReferenceEquals(aggregate.InnerExceptions[0], primary) ||
                !ReferenceEquals(aggregate.InnerExceptions[1], errors.Cleanup[0]))
                throw new Exception("F18 primary/managed release failure lost");
        }
    }

    private static void PartialAcquisitionAndRecovery()
    {
        for (int cycle = 0; cycle < Cycles; cycle++)
        for (int stage = 0; stage < 4; stage++)
        {
            var oracle = new Oracle();
            var faults = new Faults();
            ManualResetEvent? first = null, second = null;
            try
            {
                if (stage == 0) throw new IOException("primary before first");
                first = new ManualResetEvent(false);
                oracle.Create(1);
                if (stage == 1) throw new IOException("primary after first");
                second = new ManualResetEvent(false);
                oracle.Create(2);
                throw new IOException("primary after second");
            }
            catch (IOException ex) { faults.SetPrimary(ex); }
            finally
            {
                if (first is not null)
                {
                    faults.TryCleanup(() =>
                    {
                        if (stage == 2)
                        {
                            oracle.BeginClose(1);
                            try { throw new IOException("modeled callback fault"); }
                            catch
                            {
                                oracle.MarkUncertain(1);
                                throw;
                            }
                            finally { first.Dispose(); }
                        }
                        else Close(first, 1, oracle);
                    });
                }
                if (second is not null) faults.TryCleanup(() => Close(second, 2, oracle));
            }
            if (faults.Primary is not IOException || faults.Cleanup.Count != (stage == 2 ? 1 : 0))
                throw new Exception("F18 exception priority or secondary diagnostics lost");
            oracle.AssertSettled(stage == 2 ? 1 : 0);
            if (oracle.Created != oracle.Completed + oracle.Uncertain)
                throw new Exception("F18 ledger does not reconcile acquisition and cleanup");
            if (stage == 2)
            {
                try { faults.ThrowIfAny(); throw new Exception("F18 lost secondary error"); }
                catch (AggregateException ex) when (ex.InnerExceptions.Count == 2 &&
                    ReferenceEquals(ex.InnerExceptions[0], faults.Primary) &&
                    ReferenceEquals(ex.InnerExceptions[1], faults.Cleanup[0])) { }
            }
            using var recovered = new ManualResetEvent(false);
            if (!recovered.Set() || !recovered.WaitOne(0))
                throw new Exception("F18 partial-acquisition recovery failed");
        }
    }

    private static void ConsecutiveFaultRecovery()
    {
        for (int cycle = 0; cycle < Cycles; cycle++)
        {
            var oracle = new Oracle();
            for (int i = 0; i < 2; i++)
            {
                int id = cycle * 2 + i;
                using var local = new ManualResetEvent(false);
                oracle.Create(id);
                oracle.BeginClose(id);
                try { throw new IOException("modeled callback failure"); }
                catch (IOException) { oracle.MarkUncertain(id); }
                finally { local.Dispose(); }
                oracle.AssertSettled(i + 1);
                if (!local.SafeWaitHandle.IsClosed)
                    throw new Exception("F18 modeled uncertain release leaked native event");
            }
            if (oracle.Created != 2 || oracle.Uncertain != 2 || oracle.Completed != 0)
                throw new Exception("F18 chained-fault ledger mismatch");
            using var recovered = new ManualResetEvent(false);
            if (!recovered.Set() || !recovered.WaitOne(0))
                throw new Exception("F18 chained-fault recovery failed");
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
            var faults = new Faults();
            int ownedId = 1000 + cycle * 2, otherId = ownedId + 1;
            using var owned = new ManualResetEvent(false);
            using var other = new ManualResetEvent(false);
            oracle.Create(ownedId);
            oracle.Create(otherId);
            SafeWaitHandle safe = owned.SafeWaitHandle;
            var entered = Signal();
            var release = Signal();
            int taskId = cycle + 1;
            oracle.Started(taskId);
            Task worker = Task.Run(async () =>
            {
                bool added = false;
                bool recorded = false;
                try
                {
                    safe.DangerousAddRef(ref added);
                    if (!added || safe.IsInvalid) throw new Exception("F18 lease was not acquired");
                    oracle.Lease(ownedId);
                    recorded = true;
                    entered.TrySetResult(true);
                    await release.Task;
                    if (cycle % 2 == 0) throw new IOException("modeled worker fault after barrier release");
                }
                finally
                {
                    try
                    {
                        if (added)
                        {
                            safe.DangerousRelease(); // If it throws, lease stays outstanding.
                            if (recorded) oracle.ReturnLease(ownedId);
                        }
                    }
                    finally { oracle.Finished(taskId); }
                }
            });
            bool joined = false;
            try
            {
                await entered.Task.WaitAsync(Deadline);
                try
                {
                    await worker.WaitAsync(TimeSpan.Zero);
                    throw new Exception("F18 worker should still be blocked");
                }
                catch (TimeoutException ex) { faults.SetPrimary(ex); }
                if (worker.IsCompleted) throw new Exception("F18 observer timeout ended worker");
                Close(owned, ownedId, oracle); // lease remains active
                if (!other.Set() || !other.WaitOne(0))
                    throw new Exception("F18 independent event failed");
            }
            catch (Exception ex)
            {
                if (faults.Primary is null) faults.SetPrimary(ex);
                else faults.RecordCleanup(ex);
            }
            finally
            {
                release.TrySetResult(true);
                // The only worker gate has been released and its continuation is
                // finite and deterministic. Await the ACTUAL worker rather than
                // misinterpreting WaitAsync timeout as task completion. A separate
                // negative control above checks that observer timeout alone cannot
                // settle a task. This path must not abandon a pending worker.
                try
                {
                    await worker;
                    joined = true;
                }
                catch (IOException ex) when (cycle % 2 == 0 &&
                    ex.Message.Contains("worker fault", StringComparison.Ordinal))
                {
                    joined = worker.IsCompleted;
                }
                catch (Exception ex)
                {
                    joined = worker.IsCompleted;
                    faults.RecordCleanup(ex);
                }
                // Always attempt both closes even when join or a close failed.
                if (oracle.NeedsClose(otherId))
                    faults.TryCleanup(() => Close(other, otherId, oracle));
                if (oracle.NeedsClose(ownedId))
                    faults.TryCleanup(() => Close(owned, ownedId, oracle));
            }
            if (!joined || !worker.IsCompleted)
                throw faults.Combined(new TimeoutException("F18 worker completion unconfirmed"));
            if (!safe.IsClosed || !owned.SafeWaitHandle.IsClosed || !other.SafeWaitHandle.IsClosed)
                throw new Exception("F18 native resource not closed after join");
            oracle.AssertSettled();
            if (oracle.Created != 2 || oracle.Completed != 2 || oracle.Uncertain != 0 ||
                faults.Primary is not TimeoutException || faults.Cleanup.Count != 0)
                throw new Exception("F18 coordinated scenario diagnostics mismatch");
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
