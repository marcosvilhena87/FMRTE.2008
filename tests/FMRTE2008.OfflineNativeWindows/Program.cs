using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
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
    private readonly record struct ResourceSnapshot(int Handles, int Gdi, int User);

    // Strictly local Win32 diagnostics: own test process and own HWND.
    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint GetGuiResources(IntPtr ownProcessHandle, uint kind);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsWindow(IntPtr hwnd);

    private static ResourceSnapshot Snapshot()
    {
        using var self = Process.GetCurrentProcess();
        self.Refresh();
        return new ResourceSnapshot(
            self.HandleCount,
            checked((int)GetGuiResources(self.Handle, 0u)),
            checked((int)GetGuiResources(self.Handle, 1u)));
    }

    // Same criterion for real F30.3 measurements and the leak control.
    private static bool StableLast24(int after24, int after48) =>
        after48 - after24 <= 8;

    private static void NegativeLeakControl()
    {
        var deliberatelyRetained = new List<EventWaitHandle>(48);
        try
        {
            for (int i = 0; i < 24; i++)
                deliberatelyRetained.Add(new EventWaitHandle(false, EventResetMode.ManualReset));
            int after24 = Snapshot().Handles;
            for (int i = 0; i < 24; i++)
                deliberatelyRetained.Add(new EventWaitHandle(false, EventResetMode.ManualReset));
            int after48 = Snapshot().Handles;
            Check(after48 - after24 >= 20, -1,
                "negative control must create real handles owned by this test process");
            Check(!StableLast24(after24, after48), -1,
                "one leaked handle per cycle MUST fail the production trend predicate");
            Console.WriteLine($"F30.3 NEGATIVE PASS: own handles +{after48 - after24} over 24 leak cycles; detector rejects");
        }
        finally
        {
            foreach (var handle in deliberatelyRetained) handle.Dispose();
            Collect();
        }
    }

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
        IntPtr originalHwnd = IntPtr.Zero;
        try
        {
            window.Show();
            Drain();
            nativeSource = PresentationSource.FromVisual(window) as HwndSource;
            Check(window.IsVisible && window.IsLoaded, cycle, "real WPF window displayed and loaded");
            Check(nativeSource is not null && nativeSource.Handle != IntPtr.Zero,
                cycle, "nonzero native HWND created for displayed window");
            originalHwnd = nativeSource!.Handle;
            Check(IsWindow(originalHwnd), cycle,
                "Win32 independently confirms live HWND of the fictional WPF window");
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
        Check(originalHwnd != IntPtr.Zero && !IsWindow(originalHwnd), cycle,
            "Win32 independently confirms original HWND destroyed after Close");

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
        // Negative detection exercise runs before either baseline and disposes
        // all of its own local event handles afterwards.
        NegativeLeakControl();
        Collect();
        var raw = Snapshot();
        Console.WriteLine($"F30.3 RAW handles={raw.Handles} gdi={raw.Gdi} user={raw.User}");
        ResourceSnapshot previous = raw, baseline = raw;
        int stableConsecutive = 0, warmed = 0;
        // Warm up in pairs, require two successive stable resource groups,
        // and fail within eight pairs instead of changing the budget.
        for (int group = 0; group < 8; group++)
        {
            _ = ShowExerciseClose(-100 - group * 2);
            _ = ShowExerciseClose(-101 - group * 2);
            warmed += 2;
            Collect();
            baseline = Snapshot();
            int dh = baseline.Handles - previous.Handles;
            int dg = baseline.Gdi - previous.Gdi;
            int du = baseline.User - previous.User;
            bool stable = Math.Abs(dh) <= 8 && Math.Abs(dg) <= 4 && Math.Abs(du) <= 4;
            stableConsecutive = stable ? stableConsecutive + 1 : 0;
            Console.WriteLine($"F30.3 WARMUP windows={warmed} handles={baseline.Handles} gdi={baseline.Gdi} user={baseline.User} delta=({dh},{dg},{du}) stableGroups={stableConsecutive}");
            Check(postCloseCallbacks == warmed, -1, "all warm-up callbacks must be drained");
            if (stableConsecutive >= 2) break;
            previous = baseline;
        }
        Check(stableConsecutive >= 2, -1, "warm-up resources did not stabilize");
        postCloseCallbacks = 0;
        long baselineManaged = GC.GetTotalMemory(forceFullCollection: true);
        var windows = new List<WeakReference<OfflineWorkspaceWindow>>(Cycles);
        var checkpoints = new List<ResourceSnapshot>();
        int maxRetained = 0;
        long maxManagedGrowth = 0;
        for (int i = 0; i < Cycles; i++)
        {
            windows.Add(ShowExerciseClose(i));
            if ((i + 1) % 8 != 0) continue;
            Collect();
            var now = Snapshot();
            checkpoints.Add(now);
            int held = windows.Count(w => w.TryGetTarget(out _));
            long heapDelta = GC.GetTotalMemory(forceFullCollection: true) - baselineManaged;
            maxRetained = Math.Max(maxRetained, held);
            maxManagedGrowth = Math.Max(maxManagedGrowth, heapDelta);
            Console.WriteLine($"F30.3 checkpoint={i + 1} held={held} handles={now.Handles} gdi={now.Gdi} user={now.User} deltaHandles={now.Handles - baseline.Handles} rawDeltaHandles={now.Handles - raw.Handles} deltaGdi={now.Gdi - baseline.Gdi} deltaUser={now.User - baseline.User} managedDeltaBytes={heapDelta}");
            Check(postCloseCallbacks == i + 1, i, "each post-close callback ran exactly once");
            Check(held <= 4, i, "closed displayed windows remain collectible");
            Check(now.Handles <= baseline.Handles + 128, i, "own-process handles exceed +128");
            Check(now.Gdi <= baseline.Gdi + 16 && now.User <= baseline.User + 16, i,
                "own-process GDI/USER exceeds +16");
            Check(heapDelta <= 64L * 1024 * 1024, i, "managed heap exceeds +64 MiB");
            Check(!Dispatcher.CurrentDispatcher.HasShutdownStarted, i,
                "shared Dispatcher remains alive");
            if (i + 1 == Cycles)
            {
                Check(StableLast24(checkpoints[2].Handles, now.Handles), i,
                    "last 24 windows grew more than 8 handles: progressive leak");
                Check(now.Gdi - checkpoints[2].Gdi <= 4 && now.User - checkpoints[2].User <= 4,
                    i, "last 24 windows grew more than 4 GDI or USER objects");
            }
        }
        Collect();
        int finalRetained = windows.Count(w => w.TryGetTarget(out _));
        var final = Snapshot();
        Check(finalRetained <= 4, Cycles, "final weak-reference budget");
        Check(final.Handles <= baseline.Handles + 64, Cycles, "final handles exceed +64");
        Check(final.Gdi <= baseline.Gdi + 16 && final.User <= baseline.User + 16,
            Cycles, "final GDI/USER budget");
        Console.WriteLine($"F30.3 WPF PASS: {Cycles} visible native HWND cycles, {assertions} assertions, warmupWindows={warmed}, peakRetained={maxRetained}, finalRetained={finalRetained}, rawHandles={raw.Handles}, baselineHandles={baseline.Handles}, finalHandles={final.Handles}, rawGdi={raw.Gdi}, baselineGdi={baseline.Gdi}, finalGdi={final.Gdi}, rawUser={raw.User}, baselineUser={baseline.User}, finalUser={final.User}, peakManagedGrowthBytes={maxManagedGrowth}; synthetic current process only");
    }
}
