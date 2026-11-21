using System.Runtime.ExceptionServices;
using System.Windows.Threading;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using FMRTE2008.Application;
using FMRTE2008.Desktop;
using FMRTE2008.Domain;

static class F30Lifecycle
{
    private const int Cycles = 48;
    private static int assertions;
    private static int postCloseCallbacks;
    private static void Check(bool value, int cycle, string label)
    {
        if (!value) throw new InvalidOperationException($"F30.2 cycle {cycle}: {label}");
        assertions++;
    }

    private static T Find<T>(OfflineWorkspaceWindow window, string name) where T : FrameworkElement =>
        window.FindName(name) as T ?? throw new InvalidOperationException("Missing synthetic control " + name);

    private static void Click(OfflineWorkspaceWindow window, string name) =>
        Find<Button>(window, name).RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WeakReference<OfflineWorkspaceWindow> CreateExerciseClose(int cycle, bool shared = false)
    {
        var scenario = cycle % 2 == 0 ? OfflineRosterScenario.Baseline : OfflineRosterScenario.CollisionStress;
        var expected = scenario == OfflineRosterScenario.Baseline ? 3 : 6;
        var window = new OfflineWorkspaceWindow(scenario);
        var closing = 0;
        var closed = 0;
        window.Closing += (_, e) => { closing++; Check(!e.Cancel, cycle, "closing cannot cancel"); };
        window.Closed += (_, _) => closed++;
        var weak = new WeakReference<OfflineWorkspaceWindow>(window);
        try
        {
            var grid = Find<DataGrid>(window, "PlayersGrid");
            var query = Find<TextBox>(window, "QueryBox");
            var player = Find<TextBlock>(window, "PlayerText");
            var club = Find<TextBlock>(window, "ClubText");
            var reset = Find<Button>(window, "ResetButton");
            Check(grid.Items.Count == expected && grid.SelectedIndex == 0 &&
                  ((OfflineExpandedPlayer?)grid.SelectedItem)?.Profile.DemonstrationId == "DEMO-001",
                  cycle, "fresh window has only closed fixture");
            Check(player.Text.EndsWith("(DEMO-001)", StringComparison.Ordinal) &&
                  club.Text.EndsWith("(CLUB-001)", StringComparison.Ordinal),
                  cycle, "fresh selection identity independent of previous window");

            var observedResetClick = 0;
            var resetInvocationsBefore = window.ResetInvocationsForTests;
            RoutedEventHandler observer = (_, _) => observedResetClick++;
            reset.Click += observer;
            try
            {
                Click(window, "ResetButton");
                Check(observedResetClick == 1 && grid.Items.Count == expected &&
                      window.ResetInvocationsForTests == resetInvocationsBefore + 1,
                      cycle, "one routed click invokes production reset handler exactly once");
            }
            finally { reset.Click -= observer; }

            if (cycle % 3 == 0)
            {
                query.Text = "F30-2-ABSENT";
                Click(window, "SearchButton");
                Check(grid.Items.Count == 0 && grid.SelectedItem is null &&
                      Find<TextBlock>(window, "ContractText").Text == "",
                      cycle, "empty state removes previous selection");
                Click(window, "ResetButton");
                Check(grid.Items.Count == expected && grid.SelectedIndex == 0,
                      cycle, "same instance recovers fixture after empty search");
            }
            else if (cycle % 3 == 1)
            {
                Click(window, "NextButton");
                Check(((OfflineExpandedPlayer?)grid.SelectedItem)?.Profile.DemonstrationId == "DEMO-002",
                      cycle, "navigation belongs to current window");
                window.SimulateRecoverableFailureForTests();
                Check(grid.SelectedIndex == 0 && grid.Items.Count == expected,
                      cycle, "current window self-recovers after fault");
            }
            else
            {
                window.StartPersistentResetFailureForTests();
                Check(grid.Items.Count == 0 && !Find<Button>(window, "SearchButton").IsEnabled,
                      cycle, "persistent error clears current window");
                Click(window, "ResetButton");
                Check(grid.Items.Count == 0 && reset.IsEnabled,
                      cycle, "failed reset remains safely unavailable");
                // Deliberately close while the synthetic error is active.
                // A new window must never inherit this private fault flag.
            }
        }
        finally { window.Close(); }
        Check(closing == 1 && closed == 1, cycle, "Closing/Closed each fired exactly once");
        Check(!window.IsVisible, cycle, "closed window is not visible");
        var resetCountAfterClose = window.ResetInvocationsForTests;
        Check(resetCountAfterClose >= 1, cycle, "closed window recorded reset handler invocation");
        if (shared)
        {
            // Queue a callback AFTER close. Capture only WeakReference and
            // immutable counters, never a strong Window/Control reference.
            Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.Background,
                new Action(() =>
                {
                    if (weak.TryGetTarget(out var target))
                    {
                        Check(!target.IsVisible, cycle, "queued callback cannot re-show a closed window");
                        Check(target.ResetInvocationsForTests == resetCountAfterClose,
                            cycle, "queued callback must not invoke a closed window's reset handler");
                    }
                    postCloseCallbacks++;
                }));
        }
        return weak;
    }

    // An independently terminated Dispatcher avoids falsely attributing
    // thread-scoped WPF caches/queued callbacks to a window's lifetime.
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WeakReference<OfflineWorkspaceWindow> RunIsolatedSta(int cycle)
    {
        WeakReference<OfflineWorkspaceWindow>? weak = null;
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try { weak = CreateExerciseClose(cycle); }
            catch (Exception ex) { failure = ex; }
            finally { Dispatcher.CurrentDispatcher.InvokeShutdown(); }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        if (failure is not null) ExceptionDispatchInfo.Capture(failure).Throw();
        return weak ?? throw new InvalidOperationException("No synthetic window was created.");
    }

    // One Dispatcher stays alive for the entire series: model a WPF application's
    // typical reuse of the UI thread. Pump its queue before and after GC while
    // retaining ONLY weak window references.
    private static void DrainDispatcher()
    {
        var dispatcher = Dispatcher.CurrentDispatcher;
        for (var pass = 0; pass < 3; pass++)
        {
            var frame = new DispatcherFrame();
            dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle,
                new Action(() => frame.Continue = false));
            Dispatcher.PushFrame(frame);
        }
    }

    private static void CollectClosed()
    {
        DrainDispatcher();
        GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, blocking: true, compacting: true);
        GC.WaitForPendingFinalizers();
        GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, blocking: true, compacting: true);
        DrainDispatcher();
    }

    private static void RunSharedDispatcher()
    {
        var dispatcher = Dispatcher.CurrentDispatcher;
        var references = new List<WeakReference<OfflineWorkspaceWindow>>(Cycles);
        CollectClosed();
        long initialHeap = GC.GetTotalMemory(forceFullCollection: true);
        long peakHeapGrowth = 0;
        for (int i = 0; i < Cycles; i++)
        {
            references.Add(CreateExerciseClose(i, shared: true));
            if ((i + 1) % 8 != 0) continue;
            CollectClosed();
            int alive = references.Count(w => w.TryGetTarget(out _));
            long heapDelta = GC.GetTotalMemory(forceFullCollection: true) - initialHeap;
            peakHeapGrowth = Math.Max(peakHeapGrowth, heapDelta);
            Console.WriteLine($"F30.2 SHARED checkpoint={i + 1} alive={alive} heapDeltaBytes={heapDelta}");
            Check(alive <= 4, i, $"shared Dispatcher retained {alive} closed windows after checkpoint");
            Check(heapDelta <= 64L * 1024 * 1024, i,
                $"managed heap growth exceeded 64 MiB: {heapDelta}");
            Check(!dispatcher.HasShutdownStarted, i, "shared Dispatcher remains alive during measurement");
            Check(postCloseCallbacks == i + 1, i,
                "all weak-only post-close callbacks drained exactly once");
        }
        CollectClosed();
        int retained = references.Count(w => w.TryGetTarget(out _));
        Check(retained <= 4, Cycles, $"shared Dispatcher retained {retained} windows after final collection");
        Console.WriteLine($"F30.2 SHARED PASS: 48 windows, retained={retained}, peakHeapGrowthBytes={peakHeapGrowth}; same STA Dispatcher still active");
    }

    [STAThread]
    private static void Main()
    {
        var weakWindows = new List<WeakReference<OfflineWorkspaceWindow>>(Cycles);
        for (var cycle = 0; cycle < Cycles; cycle++)
            weakWindows.Add(RunIsolatedSta(cycle));

        // Collection is observed only after all local window references have
        // left their non-inlined frame; full GC does not prove native handle release.
        for (var pass = 0; pass < 3; pass++)
        {
            GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, blocking: true, compacting: true);
            GC.WaitForPendingFinalizers();
        }
        var retained = weakWindows.Count(w => w.TryGetTarget(out _));
        Check(retained <= 4, Cycles, "unexpected strong retention after 48 closed windows: retained=" + retained);
        Console.WriteLine($"F30.2 ISOLATED PASS: {Cycles} created/closed, retained={retained}");
        RunSharedDispatcher();
        Console.WriteLine($"F30.2 WPF PASS: {Cycles} isolated + {Cycles} shared created/closed, {assertions} assertions; synthetic STA only");
    }
}
