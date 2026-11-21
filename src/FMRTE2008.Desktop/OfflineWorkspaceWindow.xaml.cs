using System.Windows;
using System.Windows.Controls;
using FMRTE2008.Application;
using FMRTE2008.Domain;

namespace FMRTE2008.Desktop;

/// <summary>All displayed information originates from closed, invented F28 fixtures.</summary>
public partial class OfflineWorkspaceWindow : Window
{
    private readonly OfflineWorkspacePresenter _presenter = new();
    private bool _rendering;

    public OfflineWorkspaceWindow()
    {
        InitializeComponent();
        Render(_presenter.Search(""));
    }

    private void Search_Click(object sender, RoutedEventArgs e) =>
        Render(_presenter.Search(QueryBox.Text));

    private void Reset_Click(object sender, RoutedEventArgs e)
    {
        QueryBox.Clear();
        Render(_presenter.Search(""));
    }

    private void Next_Click(object sender, RoutedEventArgs e) =>
        Render(_presenter.NextPlayer());

    private void Previous_Click(object sender, RoutedEventArgs e) =>
        Render(_presenter.PreviousPlayer());

    private void Club_Click(object sender, RoutedEventArgs e)
    {
        var clubId = _presenter.Current().SelectedClub?.DemonstrationId;
        var view = _presenter.ShowClub(clubId);
        // The visible query reflects the exact-ID club filter, so re-searching it
        // preserves the same club even if future fixtures share similar names.
        QueryBox.Text = view.SelectedClub?.DemonstrationId ?? "";
        Render(view);
    }

    private void PlayersGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_rendering) return;
        Render(_presenter.SelectPlayer(
            (PlayersGrid.SelectedItem as OfflineExpandedPlayer)?.Profile.DemonstrationId));
    }

    private void Render(OfflineWorkspaceState view)
    {
        _rendering = true;
        try
        {
            var roster = view.Roster;
            PlayersGrid.ItemsSource = roster.VisiblePlayers;
            PlayersGrid.SelectedIndex = roster.SelectedIndex;
            NextButton.IsEnabled = roster.HasNext;
            PreviousButton.IsEnabled = roster.HasPrevious;
            ClubButton.IsEnabled = view.SelectedClub is not null;
            StatusText.Text = roster.Selected is null
                ? $"Sem seleção • {roster.VisiblePlayers.Count} registros fictícios"
                : $"Jogador {roster.SelectedIndex + 1}/{roster.VisiblePlayers.Count} • offline";

            var selected = roster.Selected;
            PlayerText.Text = selected is null ? "Nenhum jogador selecionado." :
                $"{selected.Profile.DisplayName} • {selected.Profile.Position} ({selected.Profile.DemonstrationId})";
            ClubText.Text = view.SelectedClub is null ? "Nenhum clube selecionado." :
                $"{view.SelectedClub.Name} ({view.SelectedClub.DemonstrationId})";
            CityCountryText.Text = view.SelectedClub is null ? "" :
                $"{view.SelectedClub.City} — {view.SelectedClub.Country}";
            ContractText.Text = view.SelectedContract is null ? "" :
                $"Contrato: {view.SelectedContract.StartsOn:dd/MM/yyyy} a {view.SelectedContract.EndsOn:dd/MM/yyyy}";
            WageText.Text = view.SelectedContract is null ? "" :
                $"Salário semanal fictício: {view.SelectedContract.WeeklyWage:N2} unidades";
            ClubMembersText.Text = view.SelectedClub is null ? "" :
                $"Jogadores deste clube no filtro: {view.ClubPlayers.Count}";
            TechnicalGrid.ItemsSource = view.Technical;
            MentalGrid.ItemsSource = view.Mental;
            PhysicalGrid.ItemsSource = view.Physical;
        }
        finally { _rendering = false; }
    }
}
