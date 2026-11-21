using System.Windows;
using System.Windows.Controls;
using FMRTE2008.Application;
using FMRTE2008.Domain;

namespace FMRTE2008.Desktop;

public partial class OfflineProfilesWindow : Window
{
    private readonly OfflineProfileQueryService _profiles =
        new(new DemonstrationProfileSource());

    public OfflineProfilesWindow()
    {
        InitializeComponent();
        RefreshResults();
    }

    private void Search_Click(object sender, RoutedEventArgs e) => RefreshResults();

    private void RefreshResults()
    {
        var results = _profiles.Search(SearchBox.Text);
        ProfilesGrid.ItemsSource = results.Items;
        ProfilesGrid.SelectedIndex = results.Items.Count > 0 ? 0 : -1;
        if (results.Items.Count == 0) ShowDetails(null);
    }

    private void ProfilesGrid_SelectionChanged(object sender, SelectionChangedEventArgs e) =>
        ShowDetails(ProfilesGrid.SelectedItem as OfflinePlayerProfile);

    private void ShowDetails(OfflinePlayerProfile? player)
    {
        AttributesGrid.ItemsSource = player?.Attributes;
        ClubText.Text = player is null ? "Nenhum jogador fictício selecionado." :
            $"Clube: {player.Contract.Club}";
        DatesText.Text = player is null ? "" :
            $"Vigência: {player.Contract.StartsOn:dd/MM/yyyy} a {player.Contract.EndsOn:dd/MM/yyyy}";
        WageText.Text = player is null ? "" :
            $"Salário semanal fictício: {player.Contract.WeeklyWage:N2} unidades";
    }
}
