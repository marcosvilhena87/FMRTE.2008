using System.Windows;
using System.Windows.Controls;
using FMRTE2008.Application;
using FMRTE2008.Domain;

namespace FMRTE2008.Desktop;

public partial class OfflineRosterWindow : Window
{
    private readonly OfflineRosterPresenter _presenter = new();
    private bool _refreshing;

    public OfflineRosterWindow()
    {
        InitializeComponent();
        Render(_presenter.Search(""));
    }

    private void Search_Click(object sender, RoutedEventArgs e) =>
        Render(_presenter.Search(QueryBox.Text));

    private void Previous_Click(object sender, RoutedEventArgs e) =>
        Render(_presenter.Previous());

    private void Next_Click(object sender, RoutedEventArgs e) =>
        Render(_presenter.Next());

    private void RosterGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_refreshing) return;
        Render(_presenter.Select((RosterGrid.SelectedItem as OfflineExpandedPlayer)?.Profile.DemonstrationId));
    }

    private void Render(OfflineRosterState state)
    {
        _refreshing = true;
        try
        {
            RosterGrid.ItemsSource = state.VisiblePlayers;
            RosterGrid.SelectedIndex = state.SelectedIndex;
            PreviousButton.IsEnabled = state.HasPrevious;
            NextButton.IsEnabled = state.HasNext;
            StatusText.Text = state.Selected is null
                ? $"Nenhum jogador selecionado | {state.VisiblePlayers.Count} resultado(s) fictício(s)"
                : $"Jogador {state.SelectedIndex + 1} de {state.VisiblePlayers.Count} (fictício)";
            var selected = state.Selected;
            TechnicalGrid.ItemsSource = selected?.AttributesFor(OfflineAttributeCategory.Technical);
            MentalGrid.ItemsSource = selected?.AttributesFor(OfflineAttributeCategory.Mental);
            PhysicalGrid.ItemsSource = selected?.AttributesFor(OfflineAttributeCategory.Physical);
            ClubText.Text = selected is null ? "Nenhum clube selecionado." :
                $"Clube fictício: {selected.Club.Name} | {selected.Club.City}, {selected.Club.Country}";
            ContractText.Text = selected is null ? "" :
                $"Contrato fictício: {selected.Profile.Contract.StartsOn:dd/MM/yyyy}–{selected.Profile.Contract.EndsOn:dd/MM/yyyy} | " +
                $"Salário semanal fictício: {selected.Profile.Contract.WeeklyWage:N2} unidades";
        }
        finally { _refreshing = false; }
    }
}
