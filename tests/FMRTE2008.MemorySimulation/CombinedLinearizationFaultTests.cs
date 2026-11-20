using System.ComponentModel;
using FMRTE2008.Application;

namespace FMRTE2008.MemorySimulation;

/// <summary>
/// F14 review-only adversarial overlap matrix. Tests the actual guard at
/// externally controlled await points; no OS handles, scheduler sleeps or fm.exe.
/// </summary>
internal static class CombinedLinearizationFaultTests
{
    private const int Seeds = 64;
    private const int Modes = 6;

    internal static async Task RunAsync()
    {
        int scenarios = 0, assertions = 0;
        for (int seed = 0; seed < Seeds; seed++)
        for (int mode = 0; mode < Modes; mode++)
        {
            int closes = 0, reads = 0, inspections = 0, freshCloses = 0;
            bool failIdentity = false;
            var entered = Signal();
            var release = Signal();
            var id = new ProcessHandleIdentity(13000 + seed, 638500000000000000 + seed);
            using var cts = new CancellationTokenSource();
            var guard = new NativeReadOnlyGuard(
                () =>
                {
                    Interlocked.Increment(ref inspections);
                    if (failIdentity) throw new IOException("identity query failed");
                    return id;
                },
                async (_, bytes, _) =>
                {
                    Interlocked.Increment(ref reads);
                    entered.TrySetResult(true);
                    await release.Task;
                    bytes[0] = 0x39;
                    return mode == 0 || mode == 3
                        ? new NativeReadOutcome(false, (nuint)bytes.Length, 5)
                        : new NativeReadOutcome(true, (nuint)bytes.Length, 0);
                },
                () =>
                {
                    Interlocked.Increment(ref closes);
                    if (mode == 3 || mode == 4 || mode == 5)
                        throw new IOException("close failed");
                });

            Task<byte[]> pending = guard.ReadAsync((nuint)0x1000, 8, cts.Token);
            await entered.Task;

            if (mode == 0 || mode == 4)
            {
                // Dispose cannot close while the read still owns its lease.
                guard.Dispose();
                guard.Dispose();
            }
            else if (mode == 1)
            {
                failIdentity = true;
                RejectSync<IOException>(() => guard.GetLiveHandleIdentity());
                failIdentity = false;
                guard.Dispose();
            }
            else if (mode == 2)
            {
                cts.Cancel();
                guard.Dispose();
            }

            Assert(Volatile.Read(ref closes) == 0, seed, mode,
                "closed handle before in-flight native provider returned", ref assertions);
            release.TrySetResult(true);

            if (mode == 0)
                await Reject<Win32Exception>(() => pending);
            else if (mode == 1)
                await Reject<ObjectDisposedException>(() => pending);
            else if (mode == 2)
                await Reject<OperationCanceledException>(() => pending);
            else if (mode == 3 || mode == 4)
            {
                // Exceptional cleanup can mask the primary fault. Verify
                // attempt count and poisoned state rather than exception priority.
                await Reject<IOException>(() => pending);
            }
            else
            {
                byte[] bytes = await pending;
                Assert(bytes.Length == 8 && bytes[0] == 0x39, seed, mode,
                    "healthy read did not publish exact payload", ref assertions);
            }

            if (mode == 5)
                RejectSync<IOException>(guard.Dispose);
            else
                guard.Dispose();
            guard.Dispose();
            Assert(Volatile.Read(ref closes) == 1, seed, mode,
                "handle closed zero or multiple times", ref assertions);
            Assert(Volatile.Read(ref reads) == 1, seed, mode,
                "unexpected native read calls", ref assertions);
            int oldInspections = Volatile.Read(ref inspections);
            await Reject<ObjectDisposedException>(() =>
                guard.ReadAsync((nuint)0x2000, 8, default));
            RejectSync<ObjectDisposedException>(() => guard.GetLiveHandleIdentity());
            Assert(Volatile.Read(ref reads) == 1 &&
                   Volatile.Read(ref inspections) == oldInspections, seed, mode,
                "terminal operation reached native provider", ref assertions);

            // Every recovery receives its own distinct simulated handle,
            // regardless of whether the previous cleanup callback threw.
            var next = new NativeReadOnlyGuard(
                () => id with { StartTimeUtcTicks = id.StartTimeUtcTicks + 1 },
                (_, bytes, _) =>
                {
                    bytes[0] = 0x55;
                    return Task.FromResult(
                        new NativeReadOutcome(true, (nuint)bytes.Length, 0));
                },
                () => Interlocked.Increment(ref freshCloses));
            Assert((await next.ReadAsync((nuint)0x3000, 8, default))[0] == 0x55,
                seed, mode, "new session could not recover", ref assertions);
            next.Dispose();
            next.Dispose();
            Assert(Volatile.Read(ref freshCloses) == 1 &&
                   Volatile.Read(ref closes) == 1,
                seed, mode, "recovery reused or closed an old handle", ref assertions);
            scenarios++;
        }
        Console.WriteLine($"OK: {scenarios} F14 combined-fault/exceptional-close " +
            $"interleavings, {assertions} isolation/lifetime checks (offline)");
    }

    private static TaskCompletionSource<bool> Signal() =>
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    private static void Assert(bool condition, int seed, int mode,
        string reason, ref int count)
    {
        if (!condition) throw new Exception(
            $"F14 combined seed={seed} mode={mode}: {reason}");
        count++;
    }

    private static void RejectSync<T>(Action action) where T : Exception
    {
        try { action(); }
        catch (Exception e) when (e.GetType() == typeof(T)) { return; }
        throw new Exception($"F14 expected {typeof(T).Name}");
    }

    private static async Task Reject<T>(Func<Task> action) where T : Exception
    {
        try { await action(); }
        catch (Exception e) when (e.GetType() == typeof(T)) { return; }
        throw new Exception($"F14 expected {typeof(T).Name}");
    }
}
