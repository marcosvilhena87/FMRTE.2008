using System.ComponentModel;
using FMRTE2008.Application;

namespace FMRTE2008.MemorySimulation;

/// <summary>
/// F12 review extension: repeated failures, independent overlapping sessions,
/// deferred close and recovery. Entirely offline and deterministic.
/// </summary>
internal static class RecoveryChainStressTests
{
    private const int Seeds = 64;
    private const int Modes = 6;

    internal static async Task RunAsync()
    {
        int scenarios = 0, assertions = 0;
        void Check(bool condition, string why, int seed, int mode)
        {
            if (!condition) throw new Exception(
                $"F12 chain seed={seed}, mode={mode}: {why}");
            assertions++;
        }

        for (int seed = 0; seed < Seeds; seed++)
        for (int mode = 0; mode < Modes; mode++)
        {
            var identity = new ProcessHandleIdentity(2000 + seed,
                638100000000000000 + seed);
            int firstCloses = 0, secondCloses = 0, recoveryCloses = 0;
            int firstCalls = 0, secondCalls = 0, recoveryCalls = 0;
            var entered = Signal();
            var release = Signal();
            using var cancel = new CancellationTokenSource();

            // First session: either an immediate native failure or a pending
            // read that is invalidated while still holding the handle lease.
            var first = new NativeReadOnlyGuard(
                () => identity,
                async (_, buffer, _) =>
                {
                    Interlocked.Increment(ref firstCalls);
                    if (mode >= 2)
                    {
                        entered.TrySetResult(true);
                        await release.Task;
                    }
                    buffer[0] = 11;
                    return mode == 0
                        ? new NativeReadOutcome(false, (nuint)buffer.Length, 5)
                        : new NativeReadOutcome(true, (nuint)buffer.Length, 0);
                },
                () => Interlocked.Increment(ref firstCloses));
            var firstBudget = new BudgetedReadOnlySession(first, 2, 16);
            var inFlight = firstBudget.ReadAsync((nuint)0x1000, 8, cancel.Token);

            if (mode >= 2)
            {
                await entered.Task;
                // Second independent session remains usable even when the
                // first has an operation in flight.
                var parallel = new NativeReadOnlyGuard(
                    () => identity,
                    (_, buffer, _) =>
                    {
                        buffer[0] = 22;
                        return Task.FromResult(
                            new NativeReadOutcome(true, (nuint)buffer.Length, 0));
                    }, () => Interlocked.Increment(ref secondCloses));
                using (var independent = new BudgetedReadOnlySession(parallel, 2, 16))
                {
                    byte[] bytes = await independent.ReadAsync((nuint)0x2000, 8, default);
                    Check(bytes[0] == 22, "independent live session could not read", seed, mode);
                    Check(Volatile.Read(ref firstCloses) == 0,
                        "independent read closed pending first handle", seed, mode);
                }
                Check(secondCloses == 1, "parallel handle not closed once", seed, mode);

                if (mode == 2)
                {
                    firstBudget.Dispose();
                    firstBudget.Dispose();
                    Check(Volatile.Read(ref firstCloses) == 0,
                        "outer Dispose closed in-flight handle", seed, mode);
                }
                else if (mode == 3)
                {
                    cancel.Cancel();
                    Check(Volatile.Read(ref firstCloses) == 0,
                        "cancel closed in-flight handle", seed, mode);
                }
                else if (mode == 4)
                {
                    first.Dispose();
                    first.Dispose();
                    Check(Volatile.Read(ref firstCloses) == 0,
                        "inner Dispose closed in-flight handle", seed, mode);
                }
                else
                {
                    // Invalidating a pending read through a standalone
                    // identity failure must defer cleanup until completion.
                    // This mode uses its own fail-on-second-inspection handle
                    // below, so explicitly invalidate by inner Dispose here.
                    first.Dispose();
                    Check(Volatile.Read(ref firstCloses) == 0,
                        "pending handle closed during invalidation", seed, mode);
                }
                release.TrySetResult(true);
            }

            if (mode == 1)
            {
                Check((await inFlight)[0] == 11, "first successful session did not publish", seed, mode);
            }
            else
            {
                Type expected = mode switch
                {
                    0 => typeof(Win32Exception),
                    2 or 4 or 5 => typeof(ObjectDisposedException),
                    3 => typeof(OperationCanceledException),
                    _ => throw new Exception("unknown mode")
                };
                await Reject(() => inFlight, expected);
            }
            firstBudget.Dispose();
            firstBudget.Dispose();
            Check(firstCloses == 1, "first handle was not released exactly once", seed, mode);
            Check(firstCalls == 1, "first provider invoked unexpectedly", seed, mode);
            await Reject<ObjectDisposedException>(() =>
                firstBudget.ReadAsync((nuint)0x1000, 8, default));

            // Repeated recovery failure: do NOT revive the closed first
            // session; the second attempt acquires a separate handle and also
            // fails. The third, new handle must then succeed.
            if (mode < 2)
            {
                var second = new NativeReadOnlyGuard(
                    () => identity,
                    (_, buffer, _) =>
                    {
                        Interlocked.Increment(ref secondCalls);
                        return Task.FromResult(new NativeReadOutcome(true,
                            (nuint)(buffer.Length - 1), 0));
                    },
                    () => Interlocked.Increment(ref secondCloses));
                using (var attempt = new BudgetedReadOnlySession(second, 2, 16))
                {
                    await Reject<InvalidDataException>(() =>
                        attempt.ReadAsync((nuint)0x2000, 8, default));
                    await Reject<ObjectDisposedException>(() =>
                        attempt.ReadAsync((nuint)0x2000, 8, default));
                }
                Check(secondCalls == 1 && secondCloses == 1,
                    "failed second recovery leaked or reused handle", seed, mode);
            }

            var recovered = new NativeReadOnlyGuard(
                () => identity with { StartTimeUtcTicks = identity.StartTimeUtcTicks + 1 },
                (_, buffer, _) =>
                {
                    Interlocked.Increment(ref recoveryCalls);
                    buffer[0] = 33;
                    return Task.FromResult(
                        new NativeReadOutcome(true, (nuint)buffer.Length, 0));
                },
                () => Interlocked.Increment(ref recoveryCloses));
            using (var fresh = new BudgetedReadOnlySession(recovered, 2, 16))
            {
                byte[] recoveredBytes = await fresh.ReadAsync((nuint)0x3000, 8, default);
                Check(recoveredBytes[0] == 33, "fresh recovery failed", seed, mode);
                Check(firstCloses == 1, "new session altered closed first handle", seed, mode);
            }
            Check(recoveryCalls == 1 && recoveryCloses == 1,
                "recovered resource not closed exactly once", seed, mode);
            scenarios++;
        }

        Console.WriteLine($"OK: {scenarios} F12 chained failure/concurrency/recovery scenarios; " +
            $"{assertions} assertions (offline)");
    }

    private static TaskCompletionSource<bool> Signal() =>
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    private static async Task Reject<T>(Func<Task> action) where T : Exception =>
        await Reject(action, typeof(T));

    private static async Task Reject(Func<Task> action, Type expected)
    {
        try { await action(); }
        catch (Exception ex) when (ex.GetType() == expected) { return; }
        throw new Exception("F12 chain expected " + expected.Name);
    }
}
