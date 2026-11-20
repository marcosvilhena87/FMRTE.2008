using FMRTE2008.Application;

namespace FMRTE2008.MemorySimulation;

/// <summary>Controlled interleavings, no clocks or real process handles.</summary>
internal static class BudgetDisposeRaceTests
{
    internal static async Task RunAsync()
    {
        int passed = 0;
        void Check(bool ok, string message)
        {
            if (!ok) throw new Exception("F07 race: " + message);
            passed++;
        }

        // Dispose while one read is paused: the underlying handle remains
        // valid until the read finishes, and no stale result is returned.
        var pending = new PausedRead();
        var wrapper = new BudgetedReadOnlySession(pending, 2, 16);
        var first = wrapper.ReadAsync((nuint)0x1000, 8, default);
        await pending.Entered.Task;
        wrapper.Dispose();
        Check(pending.Disposes == 0, "Disposed inner source during active read");
        await Reject<ObjectDisposedException>(() =>
            wrapper.ReadAsync((nuint)0x1000, 8, default));
        Check(pending.Calls == 1, "Disposed reader initiated additional read");
        pending.Release.TrySetResult(true);
        await Reject<ObjectDisposedException>(() => first);
        Check(pending.Disposes == 1, "Inner source not released exactly once");
        wrapper.Dispose();
        Check(pending.Disposes == 1, "Repeated disposal released twice");

        // Two outstanding reads hold two leases, even when Dispose and
        // completion interleave in an explicit controlled sequence.
        var two = new PausedRead(expectedReads: 2);
        var multi = new BudgetedReadOnlySession(two, 2, 16);
        var a = multi.ReadAsync((nuint)0x1000, 8, default);
        var b = multi.ReadAsync((nuint)0x1000, 8, default);
        await two.Entered.Task;
        await Reject<InvalidDataException>(() =>
            multi.ReadAsync((nuint)0x1000, 8, default));
        Check(two.Calls == 2, "Budget check allowed third read");
        multi.Dispose();
        Check(two.Disposes == 0, "Multi-read handle closed too early");
        two.Release.TrySetResult(true);
        await Reject<ObjectDisposedException>(() => a);
        await Reject<ObjectDisposedException>(() => b);
        Check(two.Disposes == 1, "Multi-read handle not released exactly once");

        // Cancel while pending, then Dispose: regardless of completion
        // ordering, the underlying source must not be closed prematurely.
        var canceled = new PausedRead();
        var guarded = new BudgetedReadOnlySession(canceled);
        using (var cts = new CancellationTokenSource())
        {
            var read = guarded.ReadAsync((nuint)0x1000, 8, cts.Token);
            await canceled.Entered.Task;
            cts.Cancel();
            guarded.Dispose();
            Check(canceled.Disposes == 0, "Cancellation closed active source");
            canceled.Release.TrySetResult(true);
            await Reject<OperationCanceledException>(() => read);
            Check(canceled.Disposes == 1, "Canceled source was not released");
        }

        // A canceled attempt before acquisition must not reach the source.
        var never = new PausedRead();
        using (var canceledBefore = new BudgetedReadOnlySession(never, 1, 8))
        {
            using var cts = new CancellationTokenSource();
            cts.Cancel();
            await Reject<OperationCanceledException>(() =>
                canceledBefore.ReadAsync((nuint)0x1000, 8, cts.Token));
            Check(never.Calls == 0, "Pre-canceled read reached source");
            never.Release.TrySetResult(true);
            Check((await canceledBefore.ReadAsync((nuint)0x1000, 8, default))
                .Length == 8, "Pre-cancellation wrongly consumed budget");
        }
        Check(never.Disposes == 1, "Pre-cancel source leaked");

        Console.WriteLine($"OK: {passed} F07 deterministic budget/Dispose/cancel race assertions");
    }

    private static async Task Reject<T>(Func<Task> action) where T : Exception
    {
        try { await action(); }
        catch (T) { return; }
        throw new Exception("Expected " + typeof(T).Name);
    }

    private sealed class PausedRead(int expectedReads = 1) : IProcessReadSession
    {
        public TaskCompletionSource<bool> Entered { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource<bool> Release { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        public int Calls { get; private set; }
        public int Disposes { get; private set; }

        public async Task<byte[]> ReadAsync(nuint address, int length,
            CancellationToken cancellationToken)
        {
            if (Disposes != 0) throw new ObjectDisposedException(nameof(PausedRead));
            Calls++;
            if (Calls == expectedReads) Entered.TrySetResult(true);
            await Release.Task;
            cancellationToken.ThrowIfCancellationRequested();
            return new byte[length];
        }
        public void Dispose() => Disposes++;
    }
}
