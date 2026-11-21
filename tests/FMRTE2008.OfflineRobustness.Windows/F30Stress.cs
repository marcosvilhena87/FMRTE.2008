using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using FMRTE2008.Application;
using FMRTE2008.Desktop;

static class F30Stress
{
    // Fixed sequence, no random seed, timers, process access, files or external inputs.
    // Runs against only the closed six-person CollisionStress fixture.
    internal static void Run(OfflineWorkspaceWindow window)
    {
        T Find<T>(string name) where T : FrameworkElement =>
            window.FindName(name) as T ?? throw new InvalidOperationException("Missing offline UI element: " + name);
        void Click(string name) => Find<Button>(name).RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
        var grid = Find<DataGrid>("PlayersGrid");
        var query = Find<TextBox>("QueryBox");
        var person = Find<TextBlock>("PlayerText");
        var club = Find<TextBlock>("ClubText");
        var status = Find<TextBlock>("StatusText");
        var contract = Find<TextBlock>("ContractText");
        var technical = Find<DataGrid>("TechnicalGrid");
        var mental = Find<DataGrid>("MentalGrid");
        var physical = Find<DataGrid>("PhysicalGrid");
        int checks = 0;
        void Assert(bool condition, string description, int iteration)
        {
            if (!condition) throw new InvalidOperationException(
                $"F30.1 iteration {iteration}: {description}");
            checks++;
        }
        for (var i = 0; i < 120; i++)
        {
            // A full cycle exercises search, name collision, ID-selection, precise
            // club filter, navigation bounds, empty search and explicit reset.
            Click("ResetButton");
            Assert(grid.Items.Count == 6 && grid.SelectedIndex == 0 && query.Text == "",
                "baseline fixture restored", i);
            query.Text = "Alex Exemplo";
            Click("SearchButton");
            Assert(grid.Items.Count == 3, "duplicate names remain distinct", i);
            grid.SelectedIndex = 2;
            Assert(person.Text.Contains("DEMO-005", StringComparison.Ordinal) &&
                club.Text.Contains("CLUB-002", StringComparison.Ordinal),
                "selection bound to ID, not identical display name", i);
            Click("ClubButton");
            Assert(grid.Items.Count == 2 && query.Text == "CLUB-002",
                "exact club ID filter", i);
            Click("NextButton");
            Click("NextButton");
            Assert(grid.SelectedIndex == 1 && !Find<Button>("NextButton").IsEnabled,
                "upper navigation bound", i);
            Click("PreviousButton");
            Assert(grid.SelectedIndex == 0 && !Find<Button>("PreviousButton").IsEnabled,
                "lower navigation bound", i);
            query.Text = "CLUB-001";
            Click("SearchButton");
            Assert(grid.Items.Count == 3,
                "text search includes unrelated ID inside player name", i);
            Click("ClubButton");
            Assert(grid.Items.Count == 2 && query.Text == "CLUB-001" &&
                club.Text.Contains("CLUB-001", StringComparison.Ordinal),
                "club navigation selects only exact ID", i);
            query.Text = "F30-ABSENT";
            Click("SearchButton");
            Assert(grid.Items.Count == 0 && grid.SelectedIndex == -1 &&
                contract.Text == "" && technical.Items.Count == 0 &&
                mental.Items.Count == 0 && physical.Items.Count == 0,
                "empty result clears all player details", i);

            if (i % 10 == 0)
            {
                window.SimulateRecoverableFailureForTests();
                Assert(grid.Items.Count == 6 && grid.SelectedIndex == 0 &&
                    status.Text.Contains("recuperada", StringComparison.Ordinal),
                    "recoverable exception returns to baseline", i);

                window.SimulateResetFailureForTests(2);
                Assert(grid.Items.Count == 0 && grid.SelectedIndex == -1 &&
                    !Find<Button>("SearchButton").IsEnabled &&
                    Find<Button>("ResetButton").IsEnabled &&
                    contract.Text == "" && technical.Items.Count == 0,
                    "persistent reset failure enters safe unavailable view", i);
                Click("ResetButton");
                Assert(grid.Items.Count == 0 &&
                    !Find<Button>("SearchButton").IsEnabled,
                    "repeated reset failure does not reenter recovery", i);
                Click("ResetButton");
                Assert(grid.Items.Count == 6 && grid.SelectedIndex == 0 &&
                    Find<Button>("SearchButton").IsEnabled,
                    "explicit retry restores baseline", i);
            }
        }
        Console.WriteLine($"F30.1 WPF PASS: 120 deterministic cycles, {checks} assertions, closed synthetic fixtures only");
    }
}
