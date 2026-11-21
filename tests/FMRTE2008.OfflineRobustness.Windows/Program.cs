using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using FMRTE2008.Application;
using FMRTE2008.Desktop;

static class F29Windows
{
    private static int passed;
    [STAThread]
    private static void Main()
    {
        var w = new OfflineWorkspaceWindow(OfflineRosterScenario.CollisionStress);
        try
        {
            T Find<T>(string id) where T:FrameworkElement =>
                w.FindName(id) as T ?? throw new InvalidOperationException(id);
            void Check(bool ok,string why)
            {
                if (!ok) throw new Exception("F29 WPF: "+why);
                passed++;
            }
            void Click(string name) => Find<Button>(name).RaiseEvent(
                new RoutedEventArgs(ButtonBase.ClickEvent));
            var grid=Find<DataGrid>("PlayersGrid");
            var box=Find<TextBox>("QueryBox");
            var club=Find<TextBlock>("ClubText");
            var player=Find<TextBlock>("PlayerText");
            var status=Find<TextBlock>("StatusText");
            Check(grid.IsReadOnly && !grid.CanUserAddRows && !grid.CanUserDeleteRows, "readonly");
            Check(grid.Items.Count==6 && grid.SelectedIndex==0,"six invented players");
            box.Text="CLUB-001";
            Click("SearchButton");
            Check(grid.Items.Count==3,"text-ID collision search sees the unrelated player name");
            Click("ClubButton");
            Check(grid.Items.Count==2 && box.Text=="CLUB-001","club button exact ID only");
            Check(Find<TextBlock>("ClubMembersText").Text.Contains("2"),"two shared club members");
            Click("NextButton");
            Check(grid.SelectedIndex==1 && player.Text.Contains("DEMO-004"),"second member by unique ID");
            Check(club.Text=="Clube Exemplo A (CLUB-001)","club remains consistent");
            Click("NextButton");
            Check(grid.SelectedIndex==1 && !Find<Button>("NextButton").IsEnabled,"last clamped");
            Click("PreviousButton");
            Check(grid.SelectedIndex==0 && player.Text.Contains("DEMO-001"),"previous restores first");
            box.Text="Alex Exemplo";
            Click("SearchButton");
            Check(grid.Items.Count==3,"duplicate-name players all visible");
            grid.SelectedIndex=2;
            Check(player.Text.Contains("DEMO-005") && club.Text.Contains("CLUB-002"),
                  "grid selection resolves by ID not name");
            Click("ClubButton");
            Check(grid.Items.Count==2 && box.Text=="CLUB-002","club B includes two members");
            box.Text="Clube Exemplo A";
            Click("SearchButton");
            Check(grid.Items.Count==3,"similar club names coexist in generic search");
            grid.SelectedIndex=2;
            Check(club.Text.Contains("CLUB-004"),"similar-name club selected");
            Click("ClubButton");
            Check(grid.Items.Count==1 && box.Text=="CLUB-004","near-collision club exact ID");
            box.Text="not found";
            Click("SearchButton");
            Check(grid.Items.Count==0 && grid.SelectedIndex==-1,"empty search");
            Check(!Find<Button>("ClubButton").IsEnabled &&
                  Find<TextBlock>("ContractText").Text=="" &&
                  Find<DataGrid>("TechnicalGrid").Items.Count==0 &&
                  Find<DataGrid>("MentalGrid").Items.Count==0 &&
                  Find<DataGrid>("PhysicalGrid").Items.Count==0,"no stale contract or attributes");
            w.SimulateRecoverableFailureForTests();
            Check(box.Text=="" && grid.Items.Count==6 && grid.SelectedIndex==0,
                  "synthetic WPF exception recovered to closed baseline");
            Check(status.Text.Contains("recuperada"),"recovery explicitly disclosed");
            Click("NextButton");
            Check(grid.SelectedIndex==1,"navigation works after recovery");
            Click("ResetButton");
            Check(grid.Items.Count==6 && grid.SelectedIndex==0,"reset after exception");
            // F29-R01: a failed restoration must stop without recursion, leave
            // no stale player/contract data and allow explicit retry only.
            w.SimulateResetFailureForTests(2);
            Check(grid.Items.Count==0 && grid.SelectedIndex==-1 &&
                  !Find<Button>("NextButton").IsEnabled &&
                  !Find<Button>("SearchButton").IsEnabled &&
                  Find<Button>("ResetButton").IsEnabled,
                  "persistent recovery failure enters disabled safe view");
            Check(Find<TextBlock>("ContractText").Text=="" &&
                  Find<TextBlock>("ClubText").Text=="" &&
                  Find<TextBlock>("WageText").Text=="" &&
                  Find<DataGrid>("TechnicalGrid").Items.Count==0 &&
                  Find<DataGrid>("MentalGrid").Items.Count==0 &&
                  Find<DataGrid>("PhysicalGrid").Items.Count==0,
                  "failed recovery leaks no previous player or contract");
            Check(status.Text.Contains("Falha na restauração offline"),
                  "failed recovery disclosed without exception details");
            Click("ResetButton"); // second forced reset failure, no recursive retry
            Check(grid.Items.Count==0 && !Find<Button>("SearchButton").IsEnabled &&
                  Find<Button>("ResetButton").IsEnabled,
                  "failed manual reset remains safe and does not retry internally");
            Click("NextButton");
            Check(grid.Items.Count==0, "disabled safe state does not navigate even on synthetic event");
            Click("ResetButton"); // exhausted failure injection: explicit retry succeeds
            Check(grid.Items.Count==6 && grid.SelectedIndex==0 &&
                  Find<Button>("SearchButton").IsEnabled &&
                  Find<Button>("NextButton").IsEnabled,
                  "manual reset recovers from persistent failure");
            Check(!status.Text.Contains("Falha na restauração offline"),
                  "success clears prior failure status");

            // F29-R02: the Reset_Click handler itself must be guarded.
            w.SimulateResetClickFailureForTests();
            Check(grid.Items.Count==0 && grid.SelectedIndex==-1 &&
                  !Find<Button>("SearchButton").IsEnabled &&
                  Find<Button>("ResetButton").IsEnabled,
                  "reset handler failure enters safe view without recursive fallback");
            Click("ResetButton");
            Check(grid.Items.Count==6 && grid.SelectedIndex==0,
                  "reset handler recovers after the transient fault");
            w.SimulateResetFailureForTests(1);
            Check(grid.Items.Count==0 && Find<Button>("ResetButton").IsEnabled,
                  "single failed automatic reset remains safe");
            Click("ResetButton");
            Check(grid.Items.Count==6, "recovery after one failed automatic attempt");

            F30Stress.Run(w);
            Console.WriteLine($"F29 WPF PASS: {passed} in-process STA collision and failure recovery assertions");
        }
        finally { w.Close(); }
    }
}
