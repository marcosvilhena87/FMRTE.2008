using System.Diagnostics;
using Microsoft.Win32.SafeHandles;

// F16 deliberately creates ONLY own Windows event objects and measures this process.
// No external PID inspection, process opening, native memory read/write, or fm.exe.
if (!OperatingSystem.IsWindows())
    throw new PlatformNotSupportedException("F16 requires Windows.");

const int Cycles = 512;
const int BatchSize = 64;
const int HandleGrowthTolerance = 64; // coarse CI alarm, not proof of leak freedom.

using var current = Process.GetCurrentProcess();
static int CountHandles(Process p)
{
    p.Refresh();
    return p.HandleCount;
}
static int StabilizedCount(Process p)
{
    GC.Collect();
    GC.WaitForPendingFinalizers();
    GC.Collect();
    return CountHandles(p);
}

var ledger = new ResourceLedger();
int baseline = StabilizedCount(current);
int maxBatchDelta = int.MinValue;
int lastBatchDelta = 0;
for (int cycle = 0; cycle < Cycles; cycle++)
{
    ManualResetEvent? first = null;
    ManualResetEvent? second = null;
    SafeWaitHandle? safe = null;
    bool leaseAdded = false;
    int firstId = cycle * 2;
    int secondId = firstId + 1;
    try
    {
        first = new ManualResetEvent(false);
        ledger.Acquire(firstId);
        second = new ManualResetEvent(false);
        ledger.Acquire(secondId);
        if (!second.Set() || !second.WaitOne(0))
            throw new Exception($"F16 independent event failed at cycle {cycle}");

        safe = first.SafeWaitHandle;
        if (cycle % 4 == 0)
        {
            safe.DangerousAddRef(ref leaseAdded);
            if (!leaseAdded || safe.IsInvalid)
                throw new Exception("F16 failed to acquire local SafeHandle lease");
            ledger.AcquireLease();
            // Dispose the owner while the lease keeps the native resource alive.
            DisposeEvent(first, firstId, ledger);
            first = null;
            // IsClosed can remain false until the extra SafeHandle reference is released.
            // Never infer physical closure from owner Dispose while a lease is active.
        }
        else if (cycle % 4 == 1)
        {
            try { throw new IOException("F16 injected failure"); }
            catch (IOException) { ledger.InjectFailure(); }
        }
        else if (cycle % 4 == 2)
        {
            using var cts = new CancellationTokenSource();
            cts.Cancel();
            try
            {
                cts.Token.ThrowIfCancellationRequested();
                throw new Exception("F16 missing cancellation");
            }
            catch (OperationCanceledException) { ledger.ObserveCancellation(); }
        }
    }
    finally
    {
        // Nested finally blocks preserve independent cleanup if release/validation throws.
        try
        {
            if (leaseAdded)
            {
                safe!.DangerousRelease();
                ledger.ReleaseLease(); // count only successful release
                leaseAdded = false;
                if (!safe.IsClosed)
                    throw new Exception($"F16 handle still open after owner disposal and lease release at cycle {cycle}");
            }
        }
        finally
        {
            try
            {
                if (first is not null) DisposeEvent(first, firstId, ledger);
            }
            finally
            {
                if (second is not null) DisposeEvent(second, secondId, ledger);
            }
        }
    }

    ledger.AssertBalanced();
    if ((cycle + 1) % BatchSize == 0)
    {
        int delta = StabilizedCount(current) - baseline;
        lastBatchDelta = delta;
        maxBatchDelta = Math.Max(maxBatchDelta, delta);
        if (delta > HandleGrowthTolerance)
            throw new Exception($"F16 handle growth exceeded {HandleGrowthTolerance} at cycle {cycle + 1}: {delta}");
    }
}

ledger.AssertTotals(2 * Cycles, Cycles / 4, Cycles / 4, Cycles / 4);
await FMRTE2008.WindowsResilience.F17ResourceConcurrencyTests.RunAsync();

Console.WriteLine($"OK F16 Windows: {Cycles} cycles, {ledger.Acquisitions} local events, " +
    $"{ledger.DisposeAttempts} dispose attempts, {ledger.DisposeSuccesses} completed disposals, " +
    $"{ledger.LeasesAcquired} leases released, {ledger.Faults} injected faults, " +
    $"{ledger.Cancellations} cancellations, baseline={baseline}, " +
    $"maxBatchDelta={maxBatchDelta}, lastBatchDelta={lastBatchDelta}, " +
    $"tolerance={HandleGrowthTolerance}.");

static void DisposeEvent(ManualResetEvent evt, int id, ResourceLedger ledger)
{
    ledger.AttemptDispose(id);
    evt.Dispose();
    ledger.CompleteDispose(id);
}

internal sealed class ResourceLedger
{
    private readonly HashSet<int> _acquiredIds = new();
    private readonly HashSet<int> _activeIds = new();
    private readonly HashSet<int> _attemptedIds = new();
    public int Acquisitions => _acquiredIds.Count;
    public int DisposeAttempts => _attemptedIds.Count;
    public int DisposeSuccesses { get; private set; }
    public int LeasesAcquired { get; private set; }
    public int LeasesReleased { get; private set; }
    public int Faults { get; private set; }
    public int Cancellations { get; private set; }

    public void Acquire(int id)
    {
        if (!_acquiredIds.Add(id) || !_activeIds.Add(id))
            throw new Exception($"F16 duplicate acquisition {id}");
    }
    public void AttemptDispose(int id)
    {
        if (!_activeIds.Contains(id) || !_attemptedIds.Add(id))
            throw new Exception($"F16 invalid or duplicate disposal attempt {id}");
    }
    public void CompleteDispose(int id)
    {
        if (!_attemptedIds.Contains(id) || !_activeIds.Remove(id))
            throw new Exception($"F16 disposal completion without live resource {id}");
        DisposeSuccesses++;
    }
    public void AcquireLease() => LeasesAcquired++;
    public void ReleaseLease() => LeasesReleased++;
    public void InjectFailure() => Faults++;
    public void ObserveCancellation() => Cancellations++;
    public void AssertBalanced()
    {
        if (_activeIds.Count != 0 || DisposeAttempts != DisposeSuccesses ||
            LeasesAcquired != LeasesReleased)
            throw new Exception("F16 resource or lease imbalance after cycle");
    }
    public void AssertTotals(int resources, int leases, int faults, int cancellations)
    {
        AssertBalanced();
        if (Acquisitions != resources || DisposeAttempts != resources ||
            DisposeSuccesses != resources || LeasesAcquired != leases ||
            Faults != faults || Cancellations != cancellations)
            throw new Exception("F16 final resource accounting mismatch");
    }
}
