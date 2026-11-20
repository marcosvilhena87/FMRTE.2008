using System.ComponentModel;
using FMRTE2008.Application;

namespace FMRTE2008.MemorySimulation;

/// <summary>F11 native failure and SafeHandle lifetime interleavings without Win32.</summary>
internal static class NativeReadGuardTests
{
    internal static async Task RunAsync()
    {
        int checks = 0;
        void Check(bool ok, string message)
        {
            if (!ok) throw new Exception("F11: " + message);
            checks++;
        }
        var identity = new ProcessHandleIdentity(123, 638000000000000000);
        const nuint address = 0x1000;
        const int length = 8;

        // Happy path and single disposal.
        int normalCloses = 0;
        var normal = new NativeReadOnlyGuard(
            () => identity,
            (a, buffer, ct) =>
            {
                buffer[0] = 42;
                return Task.FromResult(new NativeReadOutcome(true, (nuint)buffer.Length, 0));
            },
            () => normalCloses++);
        Check((await normal.ReadAsync(address, length, default))[0] == 42,
            "successful read did not return bytes");
        normal.Dispose();
        normal.Dispose();
        Check(normalCloses == 1, "normal disposal not exactly once");
        await Reject<ObjectDisposedException>(() => normal.ReadAsync(address, length, default));

        async Task FailedNative(string name, NativeReadOutcome outcome, Type expected)
        {
            int closes = 0, reads = 0;
            var guard = new NativeReadOnlyGuard(() => identity,
                (_, buffer, _) =>
                {
                    reads++;
                    buffer[0] = 99;
                    return Task.FromResult(outcome);
                }, () => closes++);
            await Reject(() => guard.ReadAsync(address, length, default), expected);
            Check(closes == 1, name + ": handle was not closed once");
            Check(reads == 1, name + ": wrong read count");
            await Reject<ObjectDisposedException>(() => guard.ReadAsync(address, length, default));
            guard.Dispose();
            Check(closes == 1, name + ": repeated disposal");
        }

        await FailedNative("native failure", new NativeReadOutcome(false, 0, 5),
            typeof(Win32Exception));
        await FailedNative("native failure with claimed full count",
            new NativeReadOutcome(false, length, 299), typeof(Win32Exception));
        await FailedNative("partial success", new NativeReadOutcome(true, 4, 0),
            typeof(InvalidDataException));
        await FailedNative("zero bytes", new NativeReadOutcome(true, 0, 0),
            typeof(InvalidDataException));
        await FailedNative("overreported bytes", new NativeReadOutcome(true, 9, 0),
            typeof(InvalidDataException));

        int invalidReads = 0, invalidCloses = 0;
        using (var guard = new NativeReadOnlyGuard(() => identity,
            (_, buffer, _) =>
            {
                invalidReads++;
                return Task.FromResult(new NativeReadOutcome(true, (nuint)buffer.Length, 0));
            }, () => invalidCloses++))
        {
            await Reject<ArgumentOutOfRangeException>(() => guard.ReadAsync(0, length, default));
            await Reject<ArgumentOutOfRangeException>(() => guard.ReadAsync(address, 0, default));
            await Reject<ArgumentOutOfRangeException>(() =>
                guard.ReadAsync(address, 1024 * 1024 + 1, default));
            Check(invalidReads == 0, "invalid requests reached native read");
            Check((await guard.ReadAsync(address, length, default)).Length == length,
                "argument rejection poisoned a valid handle");
        }
        Check(invalidCloses == 1, "argument validation resource leaked");

        // Identity becomes invalid on reinspection, after bytes were filled.
        int inspections = 0, mismatchCloses = 0;
        var mismatch = new NativeReadOnlyGuard(
            () => ++inspections == 1
                ? identity
                : identity with { StartTimeUtcTicks = identity.StartTimeUtcTicks + 1 },
            (_, buffer, _) =>
            {
                buffer[0] = 77;
                return Task.FromResult(new NativeReadOutcome(true, (nuint)buffer.Length, 0));
            }, () => mismatchCloses++);
        await Reject<InvalidOperationException>(() =>
            mismatch.ReadAsync(address, length, default));
        Check(mismatchCloses == 1, "identity mismatch leaked handle");
        Check(inspections == 2, "identity not inspected twice");

        // Provider exceptions fail closed regardless of exception type.
        int unexpectedCloses = 0;
        var unexpected = new NativeReadOnlyGuard(() => identity,
            (_, _, _) => throw new NotSupportedException("Native provider failure"),
            () => unexpectedCloses++);
        await Reject<NotSupportedException>(() => unexpected.ReadAsync(address, length, default));
        Check(unexpectedCloses == 1, "unexpected native failure leaked handle");

        // Deterministic concurrency: a pending provider read owns the handle.
        var entered = NewSignal();
        var release = NewSignal();
        int pendingCloses = 0, pendingCalls = 0;
        var waiting = new NativeReadOnlyGuard(() => identity,
            async (_, buffer, _) =>
            {
                pendingCalls++;
                entered.TrySetResult(true);
                await release.Task;
                buffer[0] = 55;
                return new NativeReadOutcome(true, (nuint)buffer.Length, 0);
            }, () => Interlocked.Increment(ref pendingCloses));
        var pending = waiting.ReadAsync(address, length, default);
        await entered.Task;
        await Reject<InvalidOperationException>(() =>
            waiting.ReadAsync(address, length, default));
        Check(pendingCalls == 1, "concurrent read accessed native provider");
        await Task.WhenAll(Task.Run(waiting.Dispose), Task.Run(waiting.Dispose));
        Check(Volatile.Read(ref pendingCloses) == 0, "closed handle during pending read");
        await Reject<ObjectDisposedException>(() => waiting.ReadAsync(address, length, default));
        release.TrySetResult(true);
        await Reject<ObjectDisposedException>(() => pending);
        Check(Volatile.Read(ref pendingCloses) == 1, "pending handle not released once");

        // Cancellation while pending also keeps the lease, then poisons it.
        entered = NewSignal();
        release = NewSignal();
        int canceledCloses = 0;
        using (var token = new CancellationTokenSource())
        {
            var canceled = new NativeReadOnlyGuard(() => identity,
                async (_, buffer, ct) =>
                {
                    entered.TrySetResult(true);
                    await release.Task;
                    return new NativeReadOutcome(true, (nuint)buffer.Length, 0);
                }, () => canceledCloses++);
            var read = canceled.ReadAsync(address, length, token.Token);
            await entered.Task;
            token.Cancel();
            Check(canceledCloses == 0, "cancel prematurely closed in-flight handle");
            release.TrySetResult(true);
            await Reject<OperationCanceledException>(() => read);
            Check(canceledCloses == 1, "cancel did not close handle");
            canceled.Dispose();
            Check(canceledCloses == 1, "cancel plus Dispose double closed");
        }

        // Pre-canceled requests cannot reach native provider or exhaust the session.
        int preCalls = 0, preCloses = 0;
        using (var pre = new NativeReadOnlyGuard(() => identity,
            (_, buffer, _) =>
            {
                preCalls++;
                return Task.FromResult(new NativeReadOutcome(true, (nuint)buffer.Length, 0));
            }, () => preCloses++))
        using (var token = new CancellationTokenSource())
        {
            token.Cancel();
            await Reject<OperationCanceledException>(() => pre.ReadAsync(address, length, token.Token));
            Check(preCalls == 0, "pre-canceled request reached provider");
            Check((await pre.ReadAsync(address, length, default)).Length == length,
                "pre-cancel invalidated healthy guard");
        }
        Check(preCloses == 1, "pre-canceled guard resource leak");

        // Two independent sessions, failure in one never closes the other.
        int badCloses = 0, goodCloses = 0;
        var bad = new NativeReadOnlyGuard(() => identity,
            (_, _, _) => Task.FromResult(new NativeReadOutcome(false, 0, 6)),
            () => badCloses++);
        var good = new NativeReadOnlyGuard(() => identity,
            (_, buffer, _) => Task.FromResult(
                new NativeReadOutcome(true, (nuint)buffer.Length, 0)),
            () => goodCloses++);
        await Reject<Win32Exception>(() => bad.ReadAsync(address, length, default));
        Check(badCloses == 1 && goodCloses == 0, "bad session contaminated other handle");
        Check((await good.ReadAsync(address, length, default)).Length == length,
            "independent session could not recover");
        good.Dispose();
        Check(goodCloses == 1, "recovery handle not closed once");

        Console.WriteLine($"OK: {checks} F11 native-read/handle-lifetime checks (offline)");
    }

    private static TaskCompletionSource<bool> NewSignal() =>
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    private static async Task Reject<T>(Func<Task> action) where T : Exception =>
        await Reject(action, typeof(T));

    private static async Task Reject(Func<Task> action, Type type)
    {
        try { await action(); }
        catch (Exception ex) when (ex.GetType() == type) { return; }
        throw new Exception("F11 expected " + type.Name);
    }
}
