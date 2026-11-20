using System.ComponentModel;
using FMRTE2008.Application;

namespace FMRTE2008.MemorySimulation;

/// <summary>
/// F13: deterministic finite-state trace explorer against the real F11 guard.
/// The oracle is independent from guard's private state: a poisoned/disposed
/// session must reject every future operation, and each acquired fake resource
/// must be released once. No Win32, timers, random thread scheduling or fm.exe.
/// </summary>
internal static class StateInvariantModelTests
{
    private const int Alphabet = 5;
    private const int Depth = 5;
    private const int Traces = 3125; // 5^5, exhaustive within this bounded alphabet

    internal static async Task RunAsync()
    {
        int checks = 0, steps = 0;
        for (int trace = 0; trace < Traces; trace++)
        {
            int closes = 0, reads = 0, published = 0, inspections = 0;
            bool poisoned = false, disposed = false, faultRead = false, faultIdentity = false;
            var identity = new ProcessHandleIdentity(3000 + trace, 638000000000000000 + trace);

            var session = new NativeReadOnlyGuard(
                () =>
                {
                    inspections++;
                    if (faultIdentity) throw new IOException("F13 injected identity failure");
                    return identity;
                },
                (_, buffer, _) =>
                {
                    reads++;
                    if (faultRead)
                        return Task.FromResult(new NativeReadOutcome(false, (nuint)buffer.Length, 5));
                    buffer[0] = 0x6d;
                    return Task.FromResult(new NativeReadOutcome(true, (nuint)buffer.Length, 0));
                },
                () => closes++);

            int encoded = trace;
            for (int step = 0; step < Depth; step++)
            {
                int operation = encoded % Alphabet;
                encoded /= Alphabet;
                bool dead = poisoned || disposed;
                switch (operation)
                {
                    case 0: // successful read, or rejection after terminal state
                    {
                        int oldReads = reads;
                        if (dead)
                        {
                            await Reject<ObjectDisposedException>(
                                () => session.ReadAsync((nuint)0x1000, 8, default));
                            Ensure(reads == oldReads, trace, step, "native read after terminal state");
                        }
                        else
                        {
                            byte[] data = await session.ReadAsync((nuint)0x1000, 8, default);
                            Ensure(data.Length == 8 && data[0] == 0x6d, trace, step,
                                "valid bytes lost");
                            published++;
                            Ensure(reads == oldReads + 1, trace, step, "missing native read");
                        }
                        break;
                    }
                    case 1: // fail-closed native read
                    {
                        int oldReads = reads;
                        if (dead)
                        {
                            await Reject<ObjectDisposedException>(
                                () => session.ReadAsync((nuint)0x1000, 8, default));
                            Ensure(reads == oldReads, trace, step, "read after poison");
                        }
                        else
                        {
                            faultRead = true;
                            await Reject<Win32Exception>(
                                () => session.ReadAsync((nuint)0x1000, 8, default));
                            faultRead = false;
                            poisoned = true;
                            Ensure(reads == oldReads + 1, trace, step, "missing failed read");
                        }
                        break;
                    }
                    case 2: // standalone identity inspection failure
                    {
                        int oldInspections = inspections;
                        if (dead)
                        {
                            RejectSync<ObjectDisposedException>(
                                () => session.GetLiveHandleIdentity());
                            Ensure(inspections == oldInspections, trace, step,
                                "inspection after poison");
                        }
                        else
                        {
                            faultIdentity = true;
                            RejectSync<IOException>(() => session.GetLiveHandleIdentity());
                            faultIdentity = false;
                            poisoned = true;
                            Ensure(inspections == oldInspections + 1, trace, step,
                                "missing identity failure");
                        }
                        break;
                    }
                    case 3: // Dispose is terminal and idempotent
                        session.Dispose();
                        session.Dispose();
                        disposed = true;
                        break;
                    case 4: // healthy identity query, or rejection
                    {
                        int oldInspections = inspections;
                        if (dead)
                        {
                            RejectSync<ObjectDisposedException>(
                                () => session.GetLiveHandleIdentity());
                            Ensure(inspections == oldInspections, trace, step,
                                "terminal identity query reached native code");
                        }
                        else
                        {
                            Ensure(session.GetLiveHandleIdentity() == identity, trace, step,
                                "identity was not bound");
                            Ensure(inspections == oldInspections + 1, trace, step,
                                "identity query not executed");
                        }
                        break;
                    }
                }

                if (poisoned || disposed)
                    Ensure(closes == 1, trace, step, "terminal resource not closed once");
                else
                    Ensure(closes == 0, trace, step, "live resource closed prematurely");
                Ensure(closes <= 1, trace, step, "duplicate handle close");
                steps++;
                checks += 2;
            }

            int oldPublished = published, oldReadsEnd = reads;
            session.Dispose();
            session.Dispose();
            Ensure(closes == 1, trace, Depth, "final release not exactly once");
            await Reject<ObjectDisposedException>(
                () => session.ReadAsync((nuint)0x1000, 8, default));
            Ensure(published == oldPublished && reads == oldReadsEnd, trace, Depth,
                "bytes/native activity after final Dispose");
            checks += 2;
        }

        Console.WriteLine($"OK: {Traces} F13 bounded state traces, " +
            $"{steps} state transitions, {checks} terminal/lifetime checks (offline)");
        await PendingTransitionsAsync();
    }

    private static async Task PendingTransitionsAsync()
    {
        const int Iterations = 64;
        const int Modes = 6;
        int checkedInvariants = 0;
        for (int seed = 0; seed < Iterations; seed++)
        for (int mode = 0; mode < Modes; mode++)
        {
            var entered = Signal();
            var release = Signal();
            int closes = 0, nativeCalls = 0, inspectionCalls = 0, independentCloses = 0;
            bool failIdentity = false;
            using var cancellation = new CancellationTokenSource();
            var identity = new ProcessHandleIdentity(5000 + seed, 638100000000000000 + seed);
            var guard = new NativeReadOnlyGuard(
                () =>
                {
                    inspectionCalls++;
                    if (failIdentity) throw new IOException("F13 pending identity error");
                    return identity;
                },
                async (_, buffer, _) =>
                {
                    Interlocked.Increment(ref nativeCalls);
                    entered.TrySetResult(true);
                    await release.Task;
                    buffer[0] = 0x41;
                    return mode == 2
                        ? new NativeReadOutcome(false, (nuint)buffer.Length, 5)
                        : new NativeReadOutcome(true, (nuint)buffer.Length, 0);
                },
                () => Interlocked.Increment(ref closes));
            var pending = guard.ReadAsync((nuint)0x1000, 8, cancellation.Token);
            await entered.Task;

            if (mode == 0) guard.Dispose();
            if (mode == 1) cancellation.Cancel();
            if (mode == 3)
            {
                await Reject<InvalidOperationException>(
                    () => guard.ReadAsync((nuint)0x1000, 8, default));
                Ensure(nativeCalls == 1, seed, mode, "concurrent native read");
            }
            if (mode == 4)
            {
                failIdentity = true;
                RejectSync<IOException>(() => guard.GetLiveHandleIdentity());
                failIdentity = false;
            }
            if (mode == 5)
            {
                // Another acquired resource remains independently usable.
                var parallel = new NativeReadOnlyGuard(
                    () => identity,
                    (_, data, _) =>
                    {
                        data[0] = 0x42;
                        return Task.FromResult(
                            new NativeReadOutcome(true, (nuint)data.Length, 0));
                    }, () => Interlocked.Increment(ref independentCloses));
                Ensure((await parallel.ReadAsync((nuint)0x2000, 8, default))[0] == 0x42,
                    seed, mode, "independent session contaminated");
                parallel.Dispose();
                Ensure(independentCloses == 1, seed, mode, "independent handle leak");
            }
            Ensure(Volatile.Read(ref closes) == 0, seed, mode,
                "closed native handle before pending operation finished");
            release.TrySetResult(true);

            if (mode == 0 || mode == 4)
                await Reject<ObjectDisposedException>(() => pending);
            else if (mode == 1)
                await Reject<OperationCanceledException>(() => pending);
            else if (mode == 2)
                await Reject<Win32Exception>(() => pending);
            else
                Ensure((await pending)[0] == 0x41, seed, mode,
                    "valid in-flight bytes lost");

            guard.Dispose();
            guard.Dispose();
            Ensure(Volatile.Read(ref closes) == 1, seed, mode,
                "resource not closed exactly once after pending read");
            int callsBefore = Volatile.Read(ref nativeCalls);
            await Reject<ObjectDisposedException>(
                () => guard.ReadAsync((nuint)0x1000, 8, default));
            Ensure(Volatile.Read(ref nativeCalls) == callsBefore, seed, mode,
                "read after terminal state reached provider");
            checkedInvariants += mode == 3 || mode == 5 ? 6 : 4;
        }
        Console.WriteLine($"OK: {Iterations * Modes} F13 pending-read interleavings, " +
            $"{checkedInvariants} concurrency/isolation checks (offline)");
    }

    private static TaskCompletionSource<bool> Signal() =>
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    private static void Ensure(bool ok, int trace, int step, string message)
    {
        if (!ok) throw new Exception($"F13 trace={trace}, step/mode={step}: {message}");
    }

    private static void RejectSync<T>(Action action) where T : Exception
    {
        try { action(); }
        catch (Exception e) when (e.GetType() == typeof(T)) { return; }
        throw new Exception("F13 expected " + typeof(T).Name);
    }

    private static async Task Reject<T>(Func<Task> action) where T : Exception
    {
        try { await action(); }
        catch (Exception e) when (e.GetType() == typeof(T)) { return; }
        throw new Exception("F13 expected " + typeof(T).Name);
    }
}
