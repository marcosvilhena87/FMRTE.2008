using System.ComponentModel;
using FMRTE2008.Application;

namespace FMRTE2008.MemorySimulation;

/// <summary>
/// F15 audit: resource-close attempts versus successful release and explicit
/// bounded read budgets, using only offline fixtures.
/// </summary>
internal static class ResourceBudgetAuditTests
{
    private const int Iterations = 128;
    private static TaskCompletionSource<bool> Signal() =>
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    internal static async Task RunAsync()
    {
        int attempts = 0, successfulReleases = 0, uncertainReleases = 0;
        int verified = 0;
        for (int seed = 0; seed < Iterations; seed++)
        for (int mode = 0; mode < 4; mode++)
        {
            int closeAttempts = 0, providerCalls = 0;
            bool closeThrows = mode == 1 || mode == 3;
            var entered = Signal();
            var release = Signal();
            var identity = new ProcessHandleIdentity(31000 + seed,
                638800000000000000 + seed * 4 + mode);
            var guard = new NativeReadOnlyGuard(
                () => identity,
                async (_, bytes, _) =>
                {
                    providerCalls++;
                    if (mode >= 2)
                    {
                        entered.TrySetResult(true);
                        await release.Task;
                    }
                    bytes[0] = 0x46;
                    return mode == 0 || mode == 1
                        ? new NativeReadOutcome(false, (nuint)bytes.Length, 5)
                        : new NativeReadOutcome(true, (nuint)bytes.Length, 0);
                },
                () =>
                {
                    closeAttempts++;
                    attempts++;
                    if (closeThrows)
                    {
                        uncertainReleases++;
                        throw new IOException("simulated close failed");
                    }
                    successfulReleases++;
                });

            if (mode == 0) // error in native provider + successful close
                await Reject<Win32Exception>(() =>
                    guard.ReadAsync((nuint)0x1000, 8, default));
            else if (mode == 1) // close failure masks native provider exception
                await Reject<IOException>(() =>
                    guard.ReadAsync((nuint)0x1000, 8, default));
            else
            {
                Task<byte[]> pending = guard.ReadAsync((nuint)0x1000, 8, default);
                await entered.Task;
                if (mode == 3)
                {
                    // The completion of this pending read must close exactly once;
                    // exceptional close can mask ObjectDisposedException.
                    guard.Dispose();
                    guard.Dispose();
                }
                else guard.Dispose();
                Check(closeAttempts == 0, seed, mode,
                    "premature close while in-flight", ref verified);
                release.TrySetResult(true);
                if (mode == 3)
                    await Reject<IOException>(() => pending);
                else
                    await Reject<ObjectDisposedException>(() => pending);
            }

            guard.Dispose();
            guard.Dispose();
            Check(closeAttempts == 1 && providerCalls == 1, seed, mode,
                "close retried or native provider invoked twice", ref verified);
            int prior = providerCalls;
            await Reject<ObjectDisposedException>(() =>
                guard.ReadAsync((nuint)0x2000, 8, default));
            Check(providerCalls == prior, seed, mode,
                "invalid session reached provider", ref verified);
        }
        Check(attempts == Iterations * 4 &&
              successfulReleases == Iterations * 2 &&
              uncertainReleases == Iterations * 2,
            -1, 0, "close attempt accounting mismatch", ref verified);

        Console.WriteLine($"OK: {Iterations * 4} F15 exceptional-release schedules, " +
            $"{attempts} close attempts, {successfulReleases} successful, " +
            $"{uncertainReleases} uncertain releases, {verified} assertions (offline)");
        await BudgetBoundsAsync();
    }

    private static async Task BudgetBoundsAsync()
    {
        const int Cycles = 256;
        int checkedAssertions = 0;
        for (int seed = 0; seed < Cycles; seed++)
        {
            int innerCalls = 0, closeAttempts = 0;
            var identity = new ProcessHandleIdentity(35000 + seed,
                638900000000000000 + seed);
            var guard = new NativeReadOnlyGuard(
                () => identity,
                (_, bytes, _) =>
                {
                    innerCalls++;
                    bytes[0] = 0x29;
                    return Task.FromResult(new NativeReadOutcome(
                        true, (nuint)bytes.Length, 0));
                },
                () => closeAttempts++);
            // A fresh budgeted wrapper must not carry accounting or lifetime
            // state from a previously invalidated session.
            var budget = new BudgetedReadOnlySession(guard,
                maxReads: 2, maxBytes: 16);
            byte[] first = await budget.ReadAsync((nuint)0x1000, 8, default);
            byte[] second = await budget.ReadAsync((nuint)0x2000, 8, default);
            Check(first[0] == 0x29 && second[0] == 0x29 &&
                  innerCalls == 2, seed, 0, "healthy reads lost", ref checkedAssertions);
            await Reject<InvalidDataException>(() =>
                budget.ReadAsync((nuint)0x3000, 8, default));
            Check(innerCalls == 2 && closeAttempts == 0,
                seed, 0, "budget bypassed native call limit", ref checkedAssertions);
            // Budget exhaustion rejects at the wrapper, but does not
            // automatically invalidate it: do not claim otherwise.
            budget.Dispose();
            budget.Dispose();
            Check(closeAttempts == 1, seed, 0,
                "inner resource did not close exactly once", ref checkedAssertions);
            await Reject<ObjectDisposedException>(() =>
                budget.ReadAsync((nuint)0x4000, 8, default));
            Check(innerCalls == 2 && closeAttempts == 1, seed, 0,
                "disposed wrapper reached inner resource", ref checkedAssertions);
        }

        // Dispose during a pending read must keep the resource alive until
        // the read unwinds, and must not publish bytes after invalidation.
        for (int seed = 0; seed < Cycles; seed++)
        {
            int calls = 0, closeAttempts = 0;
            var entered = Signal();
            var release = Signal();
            var identity = new ProcessHandleIdentity(40000 + seed,
                639000000000000000 + seed);
            var guard = new NativeReadOnlyGuard(
                () => identity,
                async (_, bytes, _) =>
                {
                    calls++;
                    entered.TrySetResult(true);
                    await release.Task;
                    bytes[0] = 0x7a;
                    return new NativeReadOutcome(true, (nuint)bytes.Length, 0);
                },
                () => closeAttempts++);
            var budget = new BudgetedReadOnlySession(guard,
                maxReads: 1, maxBytes: 8);
            Task<byte[]> pending = budget.ReadAsync((nuint)0x1000, 8, default);
            await entered.Task;
            budget.Dispose();
            budget.Dispose();
            Check(closeAttempts == 0 && calls == 1, seed, 1,
                "pending wrapper closed early", ref checkedAssertions);
            release.TrySetResult(true);
            await Reject<ObjectDisposedException>(() => pending);
            Check(closeAttempts == 1 && calls == 1, seed, 1,
                "pending wrapper failed to release resource", ref checkedAssertions);
            await Reject<ObjectDisposedException>(() =>
                budget.ReadAsync((nuint)0x2000, 8, default));
            Check(closeAttempts == 1 && calls == 1, seed, 1,
                "disposed wrapper called native provider", ref checkedAssertions);
        }

        Console.WriteLine($"OK: {Cycles * 2} F15 budget/Dispose scenarios, " +
            $"{checkedAssertions} quota/lifetime assertions (offline)");
    }

    private static void Check(bool condition, int seed, int mode,
        string message, ref int checks)
    {
        if (!condition) throw new Exception(
            $"F15 audit seed={seed} mode={mode}: {message}");
        checks++;
    }

    private static async Task Reject<T>(Func<Task> action) where T : Exception
    {
        try { await action(); }
        catch (Exception ex) when (ex.GetType() == typeof(T)) { return; }
        throw new Exception($"F15 expected {typeof(T).Name}");
    }
}
