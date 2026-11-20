using System.ComponentModel;
using FMRTE2008.Application;

namespace FMRTE2008.MemorySimulation;

/// <summary>
/// F14: public-call history checker with real-time precedence plus controlled
/// native-read lease schedules. Entirely offline; no native process access.
/// </summary>
internal static class LinearizabilityScheduleTests
{
    private enum Kind { Read, Inspect, Dispose }
    private enum Result { Success, Rejected, NativeFailure, IdentityFailure }
    private readonly record struct Call(Kind Kind, Result Result, int Invoked, int Returned);

    private static bool ConsistentWithSequentialSpecification(IReadOnlyList<Call> history)
    {
        // A sequential oracle, independent of NativeReadOnlyGuard private state.
        // Each public operation occupies one position, respecting real-time:
        // if A returns before B is invoked, A must precede B.
        bool[] chosen = new bool[history.Count];
        bool Search(bool usable, int depth)
        {
            if (depth == history.Count) return true;
            for (int i = 0; i < history.Count; i++)
            {
                if (chosen[i]) continue;
                bool legalOrder = true;
                for (int j = 0; j < history.Count; j++)
                {
                    if (!chosen[j] && i != j &&
                        history[j].Returned < history[i].Invoked)
                    {
                        legalOrder = false;
                        break;
                    }
                }
                if (!legalOrder) continue;
                Call call = history[i];
                bool next = usable;
                bool valid = call.Kind switch
                {
                    Kind.Dispose => call.Result == Result.Success,
                    Kind.Inspect when call.Result == Result.Success => usable,
                    Kind.Inspect when call.Result == Result.Rejected => !usable,
                    Kind.Inspect when call.Result == Result.IdentityFailure => usable,
                    Kind.Read when call.Result == Result.Success => usable,
                    Kind.Read when call.Result == Result.Rejected => !usable,
                    Kind.Read when call.Result == Result.NativeFailure => usable,
                    _ => false
                };
                if (!valid) continue;
                if (call.Kind == Kind.Dispose ||
                    call.Result == Result.NativeFailure ||
                    call.Result == Result.IdentityFailure) next = false;
                chosen[i] = true;
                if (Search(next, depth + 1)) return true;
                chosen[i] = false;
            }
            return false;
        }
        return Search(true, 0);
    }

    internal static async Task RunAsync()
    {
        const int Seeds = 64;
        const int Modes = 6;
        int scenarios = 0, assertions = 0, checkedHistories = 0;
        void Check(bool ok, int seed, int mode, string message)
        {
            if (!ok) throw new Exception($"F14 seed={seed} mode={mode}: {message}");
            assertions++;
        }

        // Adversarial public histories drive a real guard but use an external,
        // independently enumerated sequential oracle. Modes with cancellation
        // or busy rejection use explicitly separate admission-level contracts,
        // NOT a misleading claim of ordinary method linearizability.
        for (int seed = 0; seed < Seeds; seed++)
        for (int mode = 0; mode < Modes; mode++)
        {
            var identity = new ProcessHandleIdentity(10000 + seed,
                638400000000000000 + seed);
            int nativeCalls = 0, closes = 0, inspections = 0, published = 0;
            var entered = Signal();
            var release = Signal();
            var calls = new List<Call>();
            int clock = 0;
            bool failInspection = false;
            using var cts = new CancellationTokenSource();
            var guard = new NativeReadOnlyGuard(
                () =>
                {
                    Interlocked.Increment(ref inspections);
                    if (failInspection) throw new IOException("injected inspection");
                    return identity;
                },
                async (_, bytes, _) =>
                {
                    Interlocked.Increment(ref nativeCalls);
                    entered.TrySetResult(true);
                    await release.Task;
                    bytes[0] = 0x74;
                    return mode == 5
                        ? new NativeReadOutcome(false, (nuint)bytes.Length, 5)
                        : new NativeReadOutcome(true, (nuint)bytes.Length, 0);
                },
                () => Interlocked.Increment(ref closes));

            int readStart = ++clock;
            Task<byte[]> pending = guard.ReadAsync((nuint)0x1000, 8, cts.Token);
            await entered.Task;

            if (mode == 0) // Dispose during an in-flight native read
            {
                int start = ++clock;
                guard.Dispose();
                calls.Add(new Call(Kind.Dispose, Result.Success, start, ++clock));
            }
            else if (mode == 1) // Successful inspection overlapping the read
            {
                int start = ++clock;
                Check(guard.GetLiveHandleIdentity() == identity, seed, mode,
                    "identity query failed while read in flight");
                calls.Add(new Call(Kind.Inspect, Result.Success, start, ++clock));
            }
            else if (mode == 2) // Identity inspection failure poisons pending read
            {
                failInspection = true;
                int start = ++clock;
                RejectSync<IOException>(() => guard.GetLiveHandleIdentity());
                calls.Add(new Call(Kind.Inspect, Result.IdentityFailure, start, ++clock));
                failInspection = false;
            }
            else if (mode == 3) // Cancellation is a separate token-level contract
            {
                cts.Cancel();
            }
            else if (mode == 4) // Busy rejection: an admission-level contract
            {
                await Reject<InvalidOperationException>(() =>
                    guard.ReadAsync((nuint)0x2000, 8, default));
                Check(Volatile.Read(ref nativeCalls) == 1, seed, mode,
                    "overlapping read entered native provider");
            }

            Check(Volatile.Read(ref closes) == 0, seed, mode,
                "native handle closed before leased call terminated");
            int readsBeforeRelease = Volatile.Read(ref nativeCalls);
            release.TrySetResult(true);

            Result readResult;
            if (mode == 0 || mode == 2)
            {
                await Reject<ObjectDisposedException>(() => pending);
                readResult = Result.Rejected;
            }
            else if (mode == 3)
            {
                await Reject<OperationCanceledException>(() => pending);
                readResult = Result.Rejected;
            }
            else if (mode == 5)
            {
                await Reject<Win32Exception>(() => pending);
                readResult = Result.NativeFailure;
            }
            else
            {
                var data = await pending;
                Check(data.Length == 8 && data[0] == 0x74, seed, mode,
                    "successful read did not return complete bytes");
                published++;
                readResult = Result.Success;
            }
            calls.Add(new Call(Kind.Read, readResult, readStart, ++clock));
            Check(published == (mode == 1 || mode == 4 ? 1 : 0), seed, mode,
                "bytes published despite invalidation or cancellation");
            Check(Volatile.Read(ref nativeCalls) == readsBeforeRelease,
                seed, mode, "unexpected second provider call");

            int disposal = ++clock;
            guard.Dispose();
            guard.Dispose();
            calls.Add(new Call(Kind.Dispose, Result.Success, disposal, ++clock));
            Check(Volatile.Read(ref closes) == 1, seed, mode,
                "acquired resource not closed exactly once");

            int finalQueryStart = ++clock;
            RejectSync<ObjectDisposedException>(() => guard.GetLiveHandleIdentity());
            calls.Add(new Call(Kind.Inspect, Result.Rejected,
                finalQueryStart, ++clock));
            int priorReads = Volatile.Read(ref nativeCalls);
            await Reject<ObjectDisposedException>(() =>
                guard.ReadAsync((nuint)0x1000, 8, default));
            Check(priorReads == Volatile.Read(ref nativeCalls) &&
                Volatile.Read(ref closes) == 1, seed, mode,
                "terminal session reused native provider/handle");

            // Cancellation and busy outcomes are not included in this basic
            // sequential specification; they have a stronger direct check.
            if (mode != 3 && mode != 4)
            {
                Check(ConsistentWithSequentialSpecification(calls), seed, mode,
                    "public history has no valid real-time-preserving linearization");
                checkedHistories++;
            }
            scenarios++;
        }

        // Negative controls demonstrate that the oracle actually rejects
        // histories forbidden by real-time order and resource invalidation.
        var impossible = new[]
        {
            new Call(Kind.Dispose, Result.Success, 1, 2),
            new Call(Kind.Read, Result.Success, 3, 4)
        };
        var allowed = new[]
        {
            new Call(Kind.Read, Result.Success, 1, 2),
            new Call(Kind.Dispose, Result.Success, 3, 4),
            new Call(Kind.Inspect, Result.Rejected, 5, 6)
        };
        var invalidAfterInspection = new[]
        {
            new Call(Kind.Inspect, Result.IdentityFailure, 1, 2),
            new Call(Kind.Read, Result.Success, 3, 4)
        };
        var spuriousRejection = new[]
        {
            new Call(Kind.Read, Result.Rejected, 1, 2)
        };
        var invalidAfterNativeFailure = new[]
        {
            new Call(Kind.Read, Result.NativeFailure, 1, 2),
            new Call(Kind.Inspect, Result.Success, 3, 4)
        };
        var allowedOverlap = new[]
        {
            new Call(Kind.Read, Result.Rejected, 1, 6),
            new Call(Kind.Dispose, Result.Success, 2, 3),
            new Call(Kind.Inspect, Result.Rejected, 7, 8)
        };
        var invalidOrder = new[]
        {
            new Call(Kind.Read, Result.Success, 1, 2),
            new Call(Kind.Dispose, Result.Success, 3, 4),
            new Call(Kind.Inspect, Result.Success, 5, 6)
        };
        if (ConsistentWithSequentialSpecification(impossible) ||
            ConsistentWithSequentialSpecification(invalidAfterInspection) ||
            ConsistentWithSequentialSpecification(spuriousRejection) ||
            ConsistentWithSequentialSpecification(invalidAfterNativeFailure) ||
            ConsistentWithSequentialSpecification(invalidOrder) ||
            !ConsistentWithSequentialSpecification(allowed) ||
            !ConsistentWithSequentialSpecification(allowedOverlap))
            throw new Exception("F14 independent history oracle failed positive/negative controls");
        assertions += 7;

        Console.WriteLine($"OK: {scenarios} F14 controlled adversarial schedules, " +
            $"{checkedHistories} real-time history checks, {assertions} assertions (offline)");
    }

    private static TaskCompletionSource<bool> Signal() =>
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    private static void RejectSync<T>(Action action) where T : Exception
    {
        try { action(); }
        catch (Exception ex) when (ex.GetType() == typeof(T)) { return; }
        throw new Exception("F14 expected " + typeof(T).Name);
    }

    private static async Task Reject<T>(Func<Task> action) where T : Exception
    {
        try { await action(); }
        catch (Exception ex) when (ex.GetType() == typeof(T)) { return; }
        throw new Exception("F14 expected " + typeof(T).Name);
    }
}
