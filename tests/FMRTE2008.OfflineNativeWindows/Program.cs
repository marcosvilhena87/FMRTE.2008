using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using FMRTE2008.Application;
using FMRTE2008.Desktop;
using FMRTE2008.Domain;

static class F30NativeWindows
{
    private const int Cycles = 48;
    private static int assertions;
    private static int postCloseCallbacks;

    private static void Check(bool value, int cycle, string name)
    {
        if (!value) throw new InvalidOperationException($"F30.3 cycle {cycle}: {name}");
        assertions++;
    }

    private static T Find<T>(OfflineWorkspaceWindow window, string name) where T : FrameworkElement =>
        window.FindName(name) as T ?? throw new InvalidOperationException($"Missing fictitious control {name}");

    private static void Click(OfflineWorkspaceWindow window, string name) =>
        Find<Button>(window, name).RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));

    private static void Drain()
    {
        for (int i = 0; i < 3; i++)
        {
            var frame = new DispatcherFrame();
            Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle,
                new Action(() => frame.Continue = false));
            Dispatcher.PushFrame(frame);
        }
    }

    private static void Collect()
    {
        Drain();
        GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, blocking: true, compacting: true);
        GC.WaitForPendingFinalizers();
        GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, blocking: true, compacting: true);
        Drain();
    }

    private static int OwnProcessHandles()
    {
        // Process.GetCurrentProcess counts ONLY resources of this synthetic test
        // process. Never look up any other PID or process.
        using var self = Process.GetCurrentProcess();
        self.Refresh();
        return self.HandleCount;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WeakReference<OfflineWorkspaceWindow> ShowExerciseClose(int cycle)
    {
        var scenario = cycle % 2 == 0
            ? OfflineRosterScenario.Baseline : OfflineRosterScenario.CollisionStress;
        int expected = scenario == OfflineRosterScenario.Baseline ? 3 : 6;
        var window = new OfflineWorkspaceWindow(scenario);
        var weak = new WeakReference<OfflineWorkspaceWindow>(window);
        int closing = 0, closed = 0;
        window.Closing += (_, e) => { closing++; Check(!e.Cancel, cycle, "Closing is not cancelled"); };
        window.Closed += (_, _) => closed++;
        HwndSource? nativeSource = null;
        try
        {
            window.Show();
            Drain();
            nativeSource = PresentationSource.FromVisual(window) as HwndSource;
            Check(window.IsVisible && window.IsLoaded, cycle, "real WPF window displayed and loaded");
            Check(nativeSource is not null && nativeSource.Handle != IntPtr.Zero,
                cycle, "nonzero native HWND created for displayed window");
            var grid = Find<DataGrid>(window, "PlayersGrid");
            Check(grid.Items.Count == expected &&
                  ((OfflineExpandedPlayer?)grid.SelectedItem)?.Profile.DemonstrationId == "DEMO-001",
                  cycle, "visible window has exact closed synthetic fixture");
            var reset = Find<Button>(window, "ResetButton");
            int previous = window.ResetInvocationsForTests;
            Click(window, "ResetButton");
            Check(window.ResetInvocationsForTests == previous + 1,
                cycle, "one reset click invokes handler exactly once");

            switch (cycle % 3)
            {
                case 0:
                    Find<TextBox>(window, "QueryBox").Text = "F30-3-NO-MATCH";
                    Click(window, "SearchButton");
                    Check(grid.Items.Count == 0 && grid.SelectedItem is null &&
                          Find<TextBlock>(window, "ContractText").Text == "",
                        cycle, "empty result clears visible window");
                    Click(window, "ResetButton");
                    Check(grid.Items.Count == expected && grid.SelectedIndex == 0,
                        cycle, "visible window restores synthetic baseline");
                    break;
                case 1:
                    Click(window, "NextButton");
                    Check(((OfflineExpandedPlayer?)grid.SelectedItem)?.Profile.DemonstrationId == "DEMO-002",
                        cycle, "visible navigation keeps unique fictional ID");
                    window.SimulateRecoverableFailureForTests();
                    Check(grid.SelectedIndex == 0 && grid.Items.Count == expected,
                        cycle, "visible recovery resets to baseline");
                    break;
                default:
                    window.StartPersistentResetFailureForTests();
                    Check(grid.Items.Count == 0 &&
                          !Find<Button>(window, "SearchButton").IsEnabled,
                        cycle, "persistent failure hides all data");
                    Click(window, "ResetButton");
                    Check(grid.Items.Count == 0 && reset.IsEnabled,
                        cycle, "persistent reset failure stays safe");
                    break;
            }
        }
        finally { window.Close(); }

        Check(closing == 1 && closed == 1 && !window.IsVisible,
            cycle, "visible window closes once and is no longer visible");
        Check(nativeSource is not null && nativeSource.Handle == IntPtr.Zero,
            cycle, "native HWND released after Close");

        int afterCloseResets = window.ResetInvocationsForTests;
        Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.Background,
            new Action(() =>
            {
                if (weak.TryGetTarget(out var target))
                    Check(!target.IsVisible && target.ResetInvocationsForTests == afterCloseResets,
                        cycle, "post-close callback cannot re-open or reset closed window");
                postCloseCallbacks++;
            }));
        return weak;
    }

    [STAThread]
    private static void Main()
    {
        // A single WPF UI Dispatcher remains alive through all 48 visible windows.
        // Bounds are conservative regression budgets, not native leak proofs.
        Collect();
        int baselineHandles = OwnProcessHandles();
        long baselineManaged = GC.GetTotalMemory(forceFullCollection: true);
        var windows = new List<WeakReference<OfflineWorkspaceWindow>>(Cycles);
        int maxHandles = baselineHandles, maxRetained = 0;
        long maxManagedGrowth = 0;
        for (int i = 0; i < Cycles; i++)
        {
            windows.Add(ShowExerciseClose(i));
            if ((i + 1) % 8 != 0) continue;
            Collect();
            int held = windows.Count(w => w.TryGetTarget(out _));
            int handles = OwnProcessHandles();
            long heapDelta = GC.GetTotalMemory(forceFullCollection: true) - baselineManaged;
            maxHandles = Math.Max(maxHandles, handles);
            maxRetained = Math.Max(maxRetained, held);
            maxManagedGrowth = Math.Max(maxManagedGrowth, heapDelta);
            Console.WriteLine($"F30.3 checkpoint={i + 1} held={held} handles={handles} deltaHandles={handles - baselineHandles} managedDeltaBytes={heapDelta}");
            Check(postCloseCallbacks == i + 1, i, "each queued post-close callback ran exactly once");
            Check(held <= 4, i, "closed visible windows remain collectable on shared Dispatcher");
            Check(handles <= baselineHandles + 128, i, "own-process handle growth exceeds budget of 128");
            Check(heapDelta <= 64L * 1024 * 1024, i, "managed heap growth exceeds budget of 64 MiB");
            Check(!Dispatcher.CurrentDispatcher.HasShutdownStarted, i,
                "shared Dispatcher remains active through native resource measurements");
        }
        Collect();
        int finalRetained = windows.Count(w => w.TryGetTarget(out _));
        int finalHandles = OwnProcessHandles();
        Check(finalRetained <= 4, Cycles, "final weak-reference retention budget");
        Check(finalHandles <= baselineHandles + 64, Cycles,
            "final own-process handle growth exceeds budget of 64");
        Console.WriteLine($"F30.3 WPF PASS: {Cycles} visible native HWND cycles, {assertions} assertions, peakRetained={maxRetained}, finalRetained={finalRetained}, baselineHandles={baselineHandles}, peakHandles={maxHandles}, finalHandles={finalHandles}, peakManagedGrowthBytes={maxManagedGrowth}; synthetic current process only");
    }
}
