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
    private static WeakReference<OfflineWorkspaceWindow> CreateExerciseClose(int cycle)
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
            RoutedEventHandler observer = (_, _) => observedResetClick++;
            reset.Click += observer;
            try
            {
                Click(window, "ResetButton");
                Check(observedResetClick == 1 && grid.Items.Count == expected,
                      cycle, "single reset event and no duplicated effect");
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
        return weak;
    }

    [STAThread]
    private static void Main()
    {
        var weakWindows = new List<WeakReference<OfflineWorkspaceWindow>>(Cycles);
        for (var cycle = 0; cycle < Cycles; cycle++)
            weakWindows.Add(CreateExerciseClose(cycle));

        // Collection is observed only after all local window references have
        // left their non-inlined frame; full GC does not prove native handle release.
        for (var pass = 0; pass < 3; pass++)
        {
            GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, blocking: true, compacting: true);
            GC.WaitForPendingFinalizers();
        }
        var retained = weakWindows.Count(w => w.TryGetTarget(out _));
        Check(retained <= 4, Cycles, "unexpected strong retention after 48 closed windows");
        Console.WriteLine($"F30.2 WPF PASS: {Cycles} created/closed, {assertions} assertions; weak references retained={retained}; synthetic STA only");
    }
}
