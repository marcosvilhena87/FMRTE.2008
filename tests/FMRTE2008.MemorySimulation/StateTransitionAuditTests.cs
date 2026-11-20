using System.ComponentModel;
using FMRTE2008.Application;

namespace FMRTE2008.MemorySimulation;

/// <summary>
/// F13 audit: an external transition table drives expected publication,
/// lifecycle and native-call outcomes, never inspecting implementation state.
/// </summary>
internal static class StateTransitionAuditTests
{
    private enum ModelState { Active, Pending, Invalid, Disposed }
    private enum Event { Success, NativeFault, IdentityFault, Dispose, QueryIdentity, PartialRead }
    private static readonly Event[] Events =
    {
        Event.Success, Event.NativeFault, Event.IdentityFault,
        Event.Dispose, Event.QueryIdentity, Event.PartialRead
    };
    private const int Depth = 4;
    private const int Traces = 1296; // all 6^4 event sequences

    internal static async Task RunAsync()
    {
        int transitions = 0, checks = 0;
        for (int trace = 0; trace < Traces; trace++)
        {
            var identity = new ProcessHandleIdentity(6000 + trace, 638200000000000000 + trace);
            ModelState expected = ModelState.Active;
            int providerCalls = 0, inspections = 0, releases = 0;
            int expectedReads = 0, expectedInspections = 0, expectedReleases = 0;
            int expectedPublished = 0, actualPublished = 0;
            Event requested = Event.Success;

            var session = new NativeReadOnlyGuard(
                () =>
                {
                    inspections++;
                    if (requested == Event.IdentityFault)
                        throw new IOException("oracle identity failure");
                    return identity;
                },
                (_, bytes, _) =>
                {
                    providerCalls++;
                    bytes[0] = 0x51;
                    return Task.FromResult(requested switch
                    {
                        Event.NativeFault => new NativeReadOutcome(false, (nuint)bytes.Length, 5),
                        Event.PartialRead => new NativeReadOutcome(true, (nuint)(bytes.Length - 1), 0),
                        _ => new NativeReadOutcome(true, (nuint)bytes.Length, 0)
                    });
                },
                () => releases++);

            int n = trace;
            for (int step = 0; step < Depth; step++)
            {
                var e = Events[n % Events.Length];
                n /= Events.Length;
                requested = e;
                ModelState previous = expected;
                if (previous == ModelState.Active)
                {
                    if (e == Event.Dispose) expected = ModelState.Disposed;
                    else if (e == Event.NativeFault || e == Event.IdentityFault ||
                             e == Event.PartialRead)
                        expected = ModelState.Invalid;
                    // Model owns expectations independent from SUT outcomes.
                    expectedReads += e == Event.Success || e == Event.NativeFault ||
                                     e == Event.PartialRead ? 1 : 0;
                    expectedInspections += e switch
                    {
                        Event.Success => 2,
                        Event.NativeFault or Event.PartialRead or Event.IdentityFault => 1,
                        Event.QueryIdentity => 1,
                        _ => 0
                    };
                    if (expected != ModelState.Active) expectedReleases = 1;
                    if (e == Event.Success) expectedPublished++;
                }

                if (e == Event.Dispose)
                {
                    session.Dispose();
                }
                else if (e == Event.QueryIdentity || e == Event.IdentityFault)
                {
                    if (previous != ModelState.Active)
                        RejectSync<ObjectDisposedException>(() => session.GetLiveHandleIdentity());
                    else if (e == Event.IdentityFault)
                        RejectSync<IOException>(() => session.GetLiveHandleIdentity());
                    else
                        Check(session.GetLiveHandleIdentity() == identity, trace, step,
                            "healthy identity differs", ref checks);
                }
                else
                {
                    if (previous != ModelState.Active)
                        await Reject<ObjectDisposedException>(() =>
                            session.ReadAsync((nuint)0x1000, 8, default));
                    else if (e == Event.NativeFault)
                        await Reject<Win32Exception>(() =>
                            session.ReadAsync((nuint)0x1000, 8, default));
                    else if (e == Event.PartialRead)
                        await Reject<InvalidDataException>(() =>
                            session.ReadAsync((nuint)0x1000, 8, default));
                    else
                    {
                        var bytes = await session.ReadAsync((nuint)0x1000, 8, default);
                        if (bytes.Length == 8 && bytes[0] == 0x51) actualPublished++;
                        else throw new Exception("F13: corrupted bytes published");
                    }
                }

                Check(actualPublished == expectedPublished, trace, step,
                    "published count differs from model", ref checks);
                Check(providerCalls == expectedReads, trace, step,
                    "unexpected native read", ref checks);
                Check(inspections == expectedInspections, trace, step,
                    "unexpected native inspection", ref checks);
                Check(releases == expectedReleases, trace, step,
                    "resource close count differs from model", ref checks);
                transitions++;
            }
            session.Dispose();
            session.Dispose();
            Check(releases == 1, trace, Depth, "final close not exactly once", ref checks);
            await Reject<ObjectDisposedException>(() =>
                session.ReadAsync((nuint)0x1000, 8, default));
            Check(actualPublished == expectedPublished, trace, Depth,
                "bytes published after terminal Dispose", ref checks);
        }
        Console.WriteLine($"OK: {Traces} F13 independent-oracle traces, " +
            $"{transitions} transitions, {checks} model assertions (offline)");
        await ExceptionalCloseAsync();
    }

    private static async Task ExceptionalCloseAsync()
    {
        int assertions = 0;
        const int Iterations = 64;
        // Explicitly verify exactly-once attempts, even when the close
        // callback itself throws. This is not proof the OS released a handle.
        for (int seed = 0; seed < Iterations; seed++)
        {
            int closes = 0, calls = 0;
            var identity = new ProcessHandleIdentity(9000 + seed, 638300000000000000 + seed);
            var session = new NativeReadOnlyGuard(
                () => identity,
                (_, bytes, _) =>
                {
                    calls++;
                    return Task.FromResult(new NativeReadOutcome(false, 0, 5));
                },
                () =>
                {
                    closes++;
                    throw new IOException("injected close failure");
                });
            await Reject<IOException>(() =>
                session.ReadAsync((nuint)0x1000, 8, default));
            session.Dispose();
            session.Dispose();
            Check(calls == 1, seed, 0, "unexpected native provider calls", ref assertions);
            Check(closes == 1, seed, 0, "close retried after exception", ref assertions);
            await Reject<ObjectDisposedException>(() =>
                session.ReadAsync((nuint)0x1000, 8, default));
            Check(closes == 1, seed, 0, "terminal state reused handle", ref assertions);
        }
        Console.WriteLine($"OK: {Iterations} F13 exceptional-close scenarios, " +
            $"{assertions} resource lifecycle assertions (offline)");
    }

    private static void Check(bool ok, int trace, int step, string why, ref int count)
    {
        if (!ok) throw new Exception($"F13 trace={trace} step={step}: {why}");
        count++;
    }

    private static void RejectSync<T>(Action action) where T : Exception
    {
        try { action(); }
        catch (Exception e) when (e.GetType() == typeof(T)) { return; }
        throw new Exception($"F13 expected {typeof(T).Name}");
    }

    private static async Task Reject<T>(Func<Task> action) where T : Exception
    {
        try { await action(); }
        catch (Exception e) when (e.GetType() == typeof(T)) { return; }
        throw new Exception($"F13 expected {typeof(T).Name}");
    }
}
