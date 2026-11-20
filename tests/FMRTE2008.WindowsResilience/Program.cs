using System.Diagnostics;
using System.Threading;
using Microsoft.Win32.SafeHandles;

// F16 creates exclusively self-owned Windows synchronization objects.
// Never enumerate, open, read, or write another process (including fm.exe).
if (!OperatingSystem.IsWindows())
    throw new PlatformNotSupportedException("F16 requires a Windows runner.");

const int Cycles = 512;
const int MaxAllowedHandleIncrease = 64; // CI tolerance, not a proof of no leaks.
using var current = Process.GetCurrentProcess();
static int HandleCount(Process process)
{
    process.Refresh();
    return process.HandleCount;
}
static void Collect()
{
    GC.Collect();
    GC.WaitForPendingFinalizers();
    GC.Collect();
}
Collect();
int baseline = HandleCount(current);
int acquired = 0, disposed = 0, leased = 0, cancellations = 0, failures = 0;

for (int cycle = 0; cycle < Cycles; cycle++)
{
    // Only local kernel event objects; the two sessions never share ownership.
    using var first = new ManualResetEvent(false);
    using var independent = new ManualResetEvent(false);
    acquired += 2;
    if (!independent.Set() || !independent.WaitOne(0))
        throw new Exception($"F16 independent event failed at cycle {cycle}");

    SafeWaitHandle safe = first.SafeWaitHandle;
    bool added = false;
    try
    {
        if (cycle % 4 == 0)
        {
            // SafeHandle lease remains valid even after the managed owner is disposed.
            safe.DangerousAddRef(ref added);
            if (!added) throw new Exception("F16 SafeHandle lease rejected");
            leased++;
            first.Dispose();
            first.Dispose();
            if (!safe.IsClosed) throw new Exception("F16 owner not marked closed");
        }
        else if (cycle % 4 == 1)
        {
            // A failure cannot skip finally cleanup.
            try { throw new IOException("F16 injected failure"); }
            catch (IOException) { failures++; }
        }
        else if (cycle % 4 == 2)
        {
            using var token = new CancellationTokenSource();
            token.Cancel();
            try
            {
                token.Token.ThrowIfCancellationRequested();
                throw new Exception("F16 cancellation was not observed");
            }
            catch (OperationCanceledException) { cancellations++; }
        }
    }
    finally
    {
        if (added) safe.DangerousRelease();
        first.Dispose();
        independent.Dispose();
        disposed += 2;
    }
}

Collect();
int after = HandleCount(current);
int delta = after - baseline;
if (acquired != 2 * Cycles || disposed != acquired ||
    leased != Cycles / 4 || cancellations != Cycles / 4 ||
    failures != Cycles / 4)
    throw new Exception("F16 independent resource accounting mismatch");
if (delta > MaxAllowedHandleIncrease)
    throw new Exception($"F16 process handle count grew by {delta} (limit {MaxAllowedHandleIncrease})");

Console.WriteLine($"OK F16 Windows: {Cycles} cycles, {acquired} local kernel events, " +
    $"{leased} leases, {failures} injected faults, {cancellations} cancellations, " +
    $"baseline={baseline}, after={after}, delta={delta} (tolerance {MaxAllowedHandleIncrease}).");
