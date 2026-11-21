using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using FMRTE2008.Application;
using FMRTE2008.Desktop;
using FMRTE2008.Domain;

static class F30Stress
{
    // Fixed, varied operation order. No randomness, I/O, process access or live data.
    internal static void Run(OfflineWorkspaceWindow window)
    {
        T Find<T>(string id) where T : FrameworkElement =>
            window.FindName(id) as T ?? throw new InvalidOperationException("Missing synthetic UI element: " + id);
        void Click(string name) => Find<Button>(name).RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
        var grid = Find<DataGrid>("PlayersGrid");
        var query = Find<TextBox>("QueryBox");
        var player = Find<TextBlock>("PlayerText");
        var club = Find<TextBlock>("ClubText");
        var city = Find<TextBlock>("CityCountryText");
        var contract = Find<TextBlock>("ContractText");
        var wage = Find<TextBlock>("WageText");
        var members = Find<TextBlock>("ClubMembersText");
        var status = Find<TextBlock>("StatusText");
        var technical = Find<DataGrid>("TechnicalGrid");
        var mental = Find<DataGrid>("MentalGrid");
        var physical = Find<DataGrid>("PhysicalGrid");
        var next = Find<Button>("NextButton");
        var previous = Find<Button>("PreviousButton");
        var search = Find<Button>("SearchButton");
        var clubButton = Find<Button>("ClubButton");
        var reset = Find<Button>("ResetButton");
        int assertions = 0;

        void Check(bool condition, int iteration, string description)
        {
            if (!condition)
                throw new InvalidOperationException($"F30.1 iteration {iteration}: {description}");
            assertions++;
        }

        void Selected(int iteration, string expectedPlayerId, string expectedClubId)
        {
            var row = grid.SelectedItem as OfflineExpandedPlayer;
            Check(row is not null && row.Profile.DemonstrationId == expectedPlayerId,
                iteration, "DataGrid.SelectedItem must carry exact player ID");
            Check(row is not null && row.Club.DemonstrationId == expectedClubId,
                iteration, "DataGrid.SelectedItem must carry exact club ID");
            Check(player.Text.EndsWith("(" + expectedPlayerId + ")", StringComparison.Ordinal),
                iteration, "displayed player ID must match selected row exactly");
            Check(club.Text.EndsWith("(" + expectedClubId + ")", StringComparison.Ordinal),
                iteration, "displayed club ID must match selected row exactly");
            // A synthetic profile may legitimately lack a category. Compare
            // each rendered grid with its specific selected fixture, not > 0.
            Check(!string.IsNullOrEmpty(contract.Text) &&
                !string.IsNullOrEmpty(wage.Text) &&
                technical.Items.Count == row!.AttributesFor(OfflineAttributeCategory.Technical).Count &&
                mental.Items.Count == row!.AttributesFor(OfflineAttributeCategory.Mental).Count &&
                physical.Items.Count == row!.AttributesFor(OfflineAttributeCategory.Physical).Count,
                iteration, "attribute categories match the selected fixture exactly");
        }

        void Empty(int iteration)
        {
            Check(grid.Items.Count == 0 && grid.SelectedIndex == -1 && grid.SelectedItem is null,
                iteration, "empty grid and selection");
            Check(player.Text == "Nenhum jogador selecionado." &&
                club.Text == "Nenhum clube selecionado." && city.Text == "" &&
                contract.Text == "" && wage.Text == "" && members.Text == "",
                iteration, "all fields cleared in empty result");
            Check(technical.Items.Count == 0 && mental.Items.Count == 0 &&
                physical.Items.Count == 0, iteration, "all attribute grids cleared");
            Check(!next.IsEnabled && !previous.IsEnabled && !clubButton.IsEnabled,
                iteration, "navigation and club actions disabled when empty");
        }

        void Unavailable(int iteration)
        {
            Check(grid.Items.Count == 0 && grid.SelectedIndex == -1 && grid.SelectedItem is null,
                iteration, "unavailable grid and selection empty");
            Check(player.Text == "Demonstração temporariamente indisponível." &&
                club.Text == "" && city.Text == "" && contract.Text == "" &&
                wage.Text == "" && members.Text == "" && query.Text == "",
                iteration, "unavailable state exposes no stale profile or query");
            Check(technical.Items.Count == 0 && mental.Items.Count == 0 &&
                physical.Items.Count == 0, iteration, "unavailable attributes cleared");
            Check(!query.IsEnabled && !search.IsEnabled && !grid.IsEnabled &&
                !previous.IsEnabled && !next.IsEnabled && !clubButton.IsEnabled &&
                reset.IsEnabled, iteration, "only explicit reset remains enabled");
            Check(status.Text.Contains("Falha na restauração offline", StringComparison.Ordinal),
                iteration, "non-sensitive failure message");
        }

        // 3 distinct deterministic schedules repeated 40 times, with injection
        // at 12 fixed iterations. Every branch verifies exact IDs and empty state.
        for (int i = 0; i < 120; i++)
        {
            Click("ResetButton");
            Check(grid.Items.Count == 6 && grid.SelectedIndex == 0 && query.Text == "",
                i, "closed six-profile baseline restored");
            Selected(i, "DEMO-001", "CLUB-001");
            switch (i % 3)
            {
                case 0:
                    query.Text = "Alex Exemplo";
                    Click("SearchButton");
                    Check(grid.Items.Count == 3, i, "three duplicate display names");
                    grid.SelectedIndex = 2;
                    Selected(i, "DEMO-005", "CLUB-002");
                    Click("ClubButton");
                    Check(grid.Items.Count == 2 && query.Text == "CLUB-002",
                        i, "exact club ID filter after duplicate name");
                    Selected(i, "DEMO-002", "CLUB-002");
                    Click("NextButton");
                    Selected(i, "DEMO-005", "CLUB-002");
                    Click("NextButton");
                    Check(grid.SelectedIndex == 1 && !next.IsEnabled,
                        i, "last navigation clamped");
                    Click("PreviousButton");
                    Selected(i, "DEMO-002", "CLUB-002");
                    break;
                case 1:
                    query.Text = "CLUB-001";
                    Click("SearchButton");
                    Check(grid.Items.Count == 3, i,
                        "generic search must not be treated as exact club filter");
                    Selected(i, "DEMO-001", "CLUB-001");
                    Click("ClubButton");
                    Check(grid.Items.Count == 2 && query.Text == "CLUB-001",
                        i, "exact club filter excludes ID embedded in name");
                    Selected(i, "DEMO-001", "CLUB-001");
                    Click("PreviousButton");
                    Check(grid.SelectedIndex == 0 && !previous.IsEnabled,
                        i, "first navigation clamped");
                    Click("NextButton");
                    Selected(i, "DEMO-004", "CLUB-001");
                    break;
                default:
                    query.Text = "Clube Exemplo A";
                    Click("SearchButton");
                    Check(grid.Items.Count == 3, i, "similar club names coexist");
                    grid.SelectedIndex = 2;
                    Selected(i, "DEMO-006", "CLUB-004");
                    Click("ClubButton");
                    Check(grid.Items.Count == 1 && query.Text == "CLUB-004",
                        i, "near-collision club filtered exactly");
                    Selected(i, "DEMO-006", "CLUB-004");
                    Click("NextButton");
                    Check(grid.SelectedIndex == 0 && !next.IsEnabled && !previous.IsEnabled,
                        i, "singleton navigation clamped");
                    break;
            }
            // Explicitly invalidate a selection while the filtered roster
            // remains populated, then verify no stale dependent data survives.
            grid.SelectedIndex = -1;
            Check(grid.Items.Count > 0 && grid.SelectedItem is null &&
                grid.SelectedIndex == -1, i, "invalid selection keeps roster but selects nobody");
            Check(player.Text == "Nenhum jogador selecionado." &&
                club.Text == "Nenhum clube selecionado." &&
                city.Text == "" && contract.Text == "" && wage.Text == "" &&
                members.Text == "" && technical.Items.Count == 0 &&
                mental.Items.Count == 0 && physical.Items.Count == 0,
                i, "invalid selection clears all dependent fields");
            Check(!next.IsEnabled && !previous.IsEnabled && !clubButton.IsEnabled,
                i, "invalid selection cannot navigate or open a club");
            query.Text = "F30-NO-MATCH";
            Click("SearchButton");
            Empty(i);

            if (i % 10 == 0)
            {
                window.SimulateRecoverableFailureForTests();
                Check(grid.Items.Count == 6 && grid.SelectedIndex == 0 &&
                    status.Text.Contains("recuperada", StringComparison.Ordinal),
                    i, "one-shot recovered to baseline");
                Selected(i, "DEMO-001", "CLUB-001");

                // Persistent means it stays failed across *arbitrarily many*
                // explicit retries, not a finite two-element failure counter.
                window.StartPersistentResetFailureForTests();
                try
                {
                    Unavailable(i);
                    for (var retry = 0; retry < 5; retry++)
                    {
                        Click("ResetButton");
                        Unavailable(i);
                        // Synthetic routed events on disabled controls must not
                        // escape the unavailable state even if forcibly raised.
                        Click("NextButton");
                        Click("PreviousButton");
                        Click("ClubButton");
                        Click("SearchButton");
                        grid.SelectedIndex = -1;
                        Unavailable(i);
                    }
                }
                finally
                {
                    window.StopPersistentResetFailureForTests();
                }
                Click("ResetButton");
                Check(grid.Items.Count == 6 && grid.SelectedIndex == 0 &&
                    query.IsEnabled && search.IsEnabled,
                    i, "recovery requires explicit retry after fault removed");
                Selected(i, "DEMO-001", "CLUB-001");
            }
        }
        Console.WriteLine($"F30.1 WPF PASS: 120 deterministic cycles, {assertions} assertions, 3 schedules and 12 persistent failures");
    }
}
