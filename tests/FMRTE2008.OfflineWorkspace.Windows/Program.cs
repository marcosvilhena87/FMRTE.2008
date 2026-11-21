using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using FMRTE2008.Desktop;

static class WindowsSmoke
{
    private static int checks;

    [STAThread]
    private static void Main()
    {
        // No FM executable, process handle, installed game or window automation tools.
        var window = new OfflineWorkspaceWindow();
        try
        {
            T Find<T>(string name) where T : FrameworkElement =>
                window.FindName(name) as T ?? throw new InvalidOperationException(name);
            void Verify(bool good, string message)
            {
                if (!good) throw new InvalidOperationException("F28 WPF: " + message);
                checks++;
            }
            void Click(string button) => Find<Button>(button).RaiseEvent(
                new RoutedEventArgs(ButtonBase.ClickEvent));

            var players = Find<DataGrid>("PlayersGrid");
            var technical = Find<DataGrid>("TechnicalGrid");
            var mental = Find<DataGrid>("MentalGrid");
            var physical = Find<DataGrid>("PhysicalGrid");
            var search = Find<TextBox>("QueryBox");
            Verify(players.IsReadOnly && technical.IsReadOnly && mental.IsReadOnly &&
                   physical.IsReadOnly, "all grids readonly");
            Verify(players.Items.Count == 3 && players.SelectedIndex == 0, "initial fixture population");
            Verify(Find<TextBlock>("PlayerText").Text.Contains("Alex Exemplo"), "first details");
            Verify(Find<TextBlock>("ClubText").Text.Contains("Clube Exemplo A"), "initial club details");
            Verify(!Find<Button>("PreviousButton").IsEnabled, "previous disabled initially");
            Click("NextButton");
            Verify(players.SelectedIndex == 1, "next navigation");
            Verify(Find<TextBlock>("ClubText").Text.Contains("Clube Exemplo B"), "next club synchronized");
            Verify(Find<TextBlock>("WageText").Text.Contains("1"), "contract fields populated");
            Click("PreviousButton");
            Verify(players.SelectedIndex == 0, "previous navigation");
            search.Text = "Caio";
            Click("SearchButton");
            Verify(players.Items.Count == 1 && players.SelectedIndex == 0, "filtered navigation");
            Verify(Find<TextBlock>("PlayerText").Text.Contains("Caio"), "filtered detail");
            Click("ClubButton");
            Verify(players.Items.Count == 1 && Find<TextBlock>("ClubText").Text.Contains("Clube Exemplo C"),
                "club navigation");
            search.Text = "não existe";
            Click("SearchButton");
            Verify(players.Items.Count == 0 && players.SelectedIndex == -1, "empty grid");
            Verify(Find<TextBlock>("ContractText").Text == "" && Find<TextBlock>("WageText").Text == "" &&
                   Find<TextBlock>("ClubMembersText").Text == "", "empty selection clears contractual details");
            Verify(technical.Items.Count == 0 && mental.Items.Count == 0 && physical.Items.Count == 0,
                "empty selection clears attribute groups");
            Verify(!Find<Button>("NextButton").IsEnabled && !Find<Button>("ClubButton").IsEnabled,
                "empty selection disables navigation");
            Click("ResetButton");
            Verify(players.Items.Count == 3 && players.SelectedIndex == 0, "reset restores all fixtures");
            players.SelectedIndex = 2;
            Verify(Find<TextBlock>("PlayerText").Text.Contains("Caio"), "actual DataGrid selection event");
            Console.WriteLine($"F28 WPF PASS: {checks} in-process STA UI component assertions, never started fm.exe");
        }
        finally { window.Close(); }
    }
}
