using System.ComponentModel;
using FMRTE2008.Application;

namespace FMRTE2008.MemorySimulation;

/// <summary>
/// Deterministic, bounded fault injection across F07/F10/F11 read-only layers.
/// No wall clocks, random scheduling, native handles or real process reads.
/// </summary>
internal static class CombinedFaultStressTests
{
    private const int Iterations = 128;
    private const int Modes = 10;

    internal static async Task RunAsync()
    {
        int cases = 0, assertions = 0;
        void Check(bool condition, string message, int seed, int mode)
        {
            if (!condition)
                throw new Exception($"F12 seed={seed} mode={mode}: {message}");
            assertions++;
        }

        for (int seed = 0; seed < Iterations; seed++)
        {
            for (int mode = 0; mode < Modes; mode++)
            {
                var identity = new ProcessHandleIdentity(1000 + seed,
                    638000000000000000 + seed);
                int inspections = 0, nativeCalls = 0, closed = 0;
                var entered = Signal();
                var release = Signal();
                using var cancel = new CancellationTokenSource();

                var source = new NativeReadOnlyGuard(
                    () =>
                    {
                        int number = Interlocked.Increment(ref inspections);
                        if ((mode == 4 && number == 1) ||
                            (mode == 5 && number == 2) ||
                            (mode == 9 && number == 2))
                            throw new IOException("Injected identity failure");
                        return identity;
                    },
                    async (_, buffer, _) =>
                    {
                        Interlocked.Increment(ref nativeCalls);
                        if (mode >= 6)
                        {
                            entered.TrySetResult(true);
                            await release.Task; // controlled interleaving
                        }
                        buffer[0] = 0x5a;
                        return mode switch
                        {
                            1 => new NativeReadOutcome(false, (nuint)buffer.Length, 5),
                            2 => new NativeReadOutcome(true, (nuint)(buffer.Length - 1), 0),
                            3 => new NativeReadOutcome(true, (nuint)(buffer.Length + 1), 0),
                            _ => new NativeReadOutcome(true, (nuint)buffer.Length, 0)
                        };
                    },
                    () => Interlocked.Increment(ref closed));

                // F07 outer budget wraps the F11 leased, handle-bound source.
                // A failed operation never publishes bytes, and recovery must
                // allocate a NEW source rather than revive the poisoned one.
                var budget = new BudgetedReadOnlySession(source, maxReads: 3, maxBytes: 24);
                Task<byte[]> pending = budget.ReadAsync((nuint)0x1000, 8, cancel.Token);

                if (mode >= 6)
                {
                    await entered.Task;
                    if (mode == 6)
                    {
                        cancel.Cancel();
                        Check(Volatile.Read(ref closed) == 0,
                            "cancellation closed an active handle", seed, mode);
                    }
                    else if (mode == 7)
                    {
                        // The outer budget owns the inner source and defers the
                        // close while its read lease is outstanding.
                        budget.Dispose();
                        budget.Dispose();
                        Check(Volatile.Read(ref closed) == 0,
                            "Dispose closed an active handle", seed, mode);
                    }
                    else if (mode == 8)
                    {
                        await Reject<InvalidOperationException>(() =>
                            budget.ReadAsync((nuint)0x1000, 8, default));
                        Check(Volatile.Read(ref nativeCalls) == 1,
                            "concurrent read reached native provider", seed, mode);
                    }
                    else
                    {
                        await Reject<IOException>(() => Task.Run(() =>
                            source.GetLiveHandleIdentity()));
                        Check(Volatile.Read(ref closed) == 0,
                            "identity fault closed active handle", seed, mode);
                    }
                    release.TrySetResult(true);
                }

                if (mode == 0 || mode == 8)
                {
                    byte[] bytes = await pending;
                    Check(bytes.Length == 8 && bytes[0] == 0x5a,
                        "valid bytes not published", seed, mode);
                }
                else
                {
                    Type expected = mode switch
                    {
                        1 => typeof(Win32Exception),
                        2 or 3 => typeof(InvalidDataException),
                        4 or 5 => typeof(IOException),
                        6 => typeof(OperationCanceledException),
                        7 or 9 => typeof(ObjectDisposedException),
                        _ => throw new Exception("Unknown fault mode")
                    };
                    await Reject(() => pending, expected);
                }

                budget.Dispose();
                budget.Dispose();
                Check(Volatile.Read(ref closed) == 1,
                    "acquired native handle not closed exactly once", seed, mode);
                Check(Volatile.Read(ref nativeCalls) == (mode == 4 ? 0 : 1),
                    "incorrect native call count", seed, mode);
                await Reject<ObjectDisposedException>(() =>
                    budget.ReadAsync((nuint)0x1000, 8, default));
                Check(Volatile.Read(ref closed) == 1,
                    "rejected later read triggered extra close", seed, mode);

                // Recovery must not reuse the old process handle.
                int recoveredCloses = 0;
                var recovered = new NativeReadOnlyGuard(
                    () => identity,
                    (_, buffer, _) =>
                    {
                        buffer[0] = 0x7b;
                        return Task.FromResult(
                            new NativeReadOutcome(true, (nuint)buffer.Length, 0));
                    },
                    () => recoveredCloses++);
                using (var newBudget = new BudgetedReadOnlySession(recovered, 2, 16))
                {
                    var bytes = await newBudget.ReadAsync((nuint)0x2000, 8, default);
                    Check(bytes[0] == 0x7b, "fresh session could not recover", seed, mode);
                }
                Check(recoveredCloses == 1,
                    "fresh recovery handle not closed exactly once", seed, mode);
                Check(Volatile.Read(ref closed) == 1,
                    "recovery interfered with failed session", seed, mode);
                cases++;
            }
        }
        Console.WriteLine($"OK: {cases} F12 deterministic fault-injection scenarios; " +
            $"{assertions} lifecycle/isolation assertions (offline)");
    }

    private static TaskCompletionSource<bool> Signal() =>
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    private static async Task Reject<T>(Func<Task> action) where T : Exception =>
        await Reject(action, typeof(T));

    private static async Task Reject(Func<Task> action, Type expected)
    {
        try { await action(); }
        catch (Exception ex) when (ex.GetType() == expected) { return; }
        throw new Exception("F12 expected " + expected.Name);
    }
}
