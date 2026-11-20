using System.Buffers.Binary;
using FMRTE2008.Application;

namespace FMRTE2008.MemorySimulation;

/// <summary>Deterministic, offline interleavings. Never contacts fm.exe.</summary>
internal static class SessionRaceTests
{
    public static async Task RunAsync()
    {
        int passed = 0;
        void Check(bool ok, string reason)
        {
            if (!ok) throw new Exception(reason);
            passed++;
        }

        // Two scans must never use and commit the same cursor concurrently.
        var gate = new PausedSource();
        var controller = new IncrementalSearchController(() => gate);
        var running = controller.NextAsync(1);
        await gate.Entered.Task;
        await Reject<InvalidOperationException>(() => controller.NextAsync(1));
        Check(controller.NextIndex == 0 && controller.Matches.Count == 0,
            "Overlapping scan published data");
        gate.Release.SetResult(true);
        await running;
        Check(controller.NextIndex == 1 && controller.Matches.Count == 1,
            "Successful scan not committed once");

        // Published snapshots must not be backed by a collection that Restart
        // can clear or a later scan can append to.
        var snapshotBeforeRestart = controller.Matches;
        controller.Restart("");
        Check(snapshotBeforeRestart.Count == 1 && snapshotBeforeRestart[0].Id == 111 &&
            controller.Matches.Count == 0 && controller.NextIndex == 0,
            "Restart changed an already published result snapshot");

        // Concurrent inspection of results is permitted while a scan is paused.
        var pending = new PausedSource();
        var observable = new IncrementalSearchController(() => pending);
        var scanning = observable.NextAsync(1);
        await pending.Entered.Task;
        Check(observable.Matches.Count == 0 && observable.NextIndex == 0,
            "Pending scan exposed uncommitted results");
        pending.Release.SetResult(true);
        await scanning;
        var committed = observable.Matches;
        observable.Restart("");
        Check(committed.Count == 1 && observable.Matches.Count == 0,
            "Published snapshot was mutated by session reset");

        // A restart while an old async read is pending must drop old data.
        var old = new PausedSource();
        var fresh = new ImmediateSource();
        int creations = 0;
        var restarted = new IncrementalSearchController(() =>
            ++creations == 1 ? old : fresh);
        var oldScan = restarted.NextAsync(1);
        await old.Entered.Task;
        restarted.Restart("");
        old.Release.SetResult(true);
        await Reject<InvalidOperationException>(() => oldScan);
        Check(!restarted.IsFailed && restarted.NextIndex == 0 &&
            restarted.Matches.Count == 0 && creations == 2,
            "Stale scan leaked results or poisoned the replacement session");
        await restarted.NextAsync(1);
        Check(restarted.NextIndex == 1 && restarted.Matches.Count == 1 &&
            restarted.Matches[0].Id == 900,
            "New session could not commit its own result");

        // A fatal failure after an earlier successful batch must invalidate
        // both the cursor and published results, not merely set IsFailed.
        var fail = new IncrementalSearchController(() => new FailOnSecondSource());
        await fail.NextAsync(1);
        Check(fail.NextIndex == 1 && fail.Matches.Count == 1, "Setup batch failed");
        await Reject<IOException>(() => fail.NextAsync(1));
        Check(fail.IsFailed && fail.NextIndex == 0 &&
            fail.Matches.Count == 0 && fail.Failures.Count == 0,
            "Fatal failure left previously published session data");

        // ABA demonstration: a pointer changes A->B->A entirely between
        // observations. The read-only F02 guard cannot detect this history.
        // This is a documented limitation, NOT proof of atomicity.
        uint pointer = 0x3000;
        var sequence = new List<uint> { pointer };
        pointer = 0x4000;
        sequence.Add(pointer);
        pointer = 0x3000;
        sequence.Add(pointer);
        ReadOnlyRecordConsistency.ConfirmUnchanged(
            (address, length) =>
            {
                if (address == 0x1000 && length == 8)
                {
                    byte[] header = new byte[8];
                    BinaryPrimitives.WriteUInt32LittleEndian(header, 0x2000);
                    BinaryPrimitives.WriteInt32LittleEndian(header.AsSpan(4), 1);
                    return header;
                }
                if (address == 0x2000 && length == 4)
                {
                    byte[] slot = new byte[4];
                    BinaryPrimitives.WriteUInt32LittleEndian(slot, pointer);
                    return slot;
                }
                throw new InvalidDataException("Unexpected fixture read");
            }, 0x1000, 0x2000, 1, 0, 0x3000);
        Check(sequence.SequenceEqual(new uint[] { 0x3000, 0x4000, 0x3000 }),
            "ABA history fixture is wrong");
        Console.WriteLine($"OK: {passed} F03 assertions for concurrent scans, restart, invalidation and ABA limitation");
    }

    private static async Task Reject<T>(Func<Task> fn) where T : Exception
    {
        try { await fn(); }
        catch (T) { return; }
        throw new Exception($"Expected {typeof(T).Name}");
    }

    private static MinimalPlayerIdentity Player(int index, int id) =>
        new(index, 0x3000, id, "Fixture", "Player", null);

    private sealed class PausedSource : IIndexedPlayerSource
    {
        public int Count => 2;
        public bool IsLiveProcessSource => false;
        public TaskCompletionSource<bool> Entered { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource<bool> Release { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public async Task<MinimalPlayerIdentity> ReadAtAsync(int index, CancellationToken ct)
        {
            Entered.TrySetResult(true);
            await Release.Task.WaitAsync(ct);
            return Player(index, 111);
        }
    }

    private sealed class ImmediateSource : IIndexedPlayerSource
    {
        public int Count => 2;
        public bool IsLiveProcessSource => false;
        public Task<MinimalPlayerIdentity> ReadAtAsync(int index, CancellationToken ct) =>
            Task.FromResult(Player(index, 900));
    }

    private sealed class FailOnSecondSource : IIndexedPlayerSource
    {
        public int Count => 2;
        public bool IsLiveProcessSource => false;
        public Task<MinimalPlayerIdentity> ReadAtAsync(int index, CancellationToken ct) =>
            index == 1 ? throw new IOException("Simulated process loss") :
                Task.FromResult(Player(index, 111));
    }
}
