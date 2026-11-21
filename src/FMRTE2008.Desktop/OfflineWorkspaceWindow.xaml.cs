using System.Windows;
using System.Windows.Controls;
using FMRTE2008.Application;
using FMRTE2008.Domain;

namespace FMRTE2008.Desktop;

/// <summary>All displayed information originates from closed, invented F28 fixtures.</summary>
public partial class OfflineWorkspaceWindow : Window
{
    private readonly OfflineWorkspacePresenter _presenter;
    private bool _rendering;
    private bool _unavailable;
    private int _resetFailuresForTests;
    private bool _persistentResetFailureForTests;

    public OfflineWorkspaceWindow() : this(OfflineRosterScenario.Baseline) { }

    internal OfflineWorkspaceWindow(OfflineRosterScenario scenario)
    {
        _presenter = new OfflineWorkspacePresenter(scenario);
        InitializeComponent();
        Render(_presenter.Current());
    }

    // One-shot recovery: never call RunSafely from its own catch block.
    // A reset error enters a safe, non-interactive screen until an explicit retry.
    private OfflineWorkspaceState RestoreBaseline()
    {
        if (_persistentResetFailureForTests)
            throw new InvalidOperationException("Synthetic persistent reset failure.");
        if (_resetFailuresForTests > 0)
        {
            _resetFailuresForTests--;
            throw new InvalidOperationException("Synthetic reset failure.");
        }
        var result = _presenter.Search("");
        QueryBox.Clear();
        return result;
    }

    private void RunSafely(Func<OfflineWorkspaceState> action, bool isReset = false)
    {
        if (_unavailable && !isReset) return;
        try
        {
            Render(action());
            _unavailable = false;
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            if (!isReset)
            {
                try
                {
                    // Exactly one restoration attempt; never reenter this method.
                    Render(RestoreBaseline());
                    _unavailable = false;
                    StatusText.Text = "Falha na demonstração recuperada; filtro e seleção reiniciados.";
                    return;
                }
                catch (Exception resetException) when
                    (resetException is ArgumentException or InvalidOperationException)
                {
                    // No repeated attempts inside the same UI action.
                }
            }
            ShowUnavailable();
        }
    }

    private void ShowUnavailable()
    {
        _unavailable = true;
        _rendering = true;
        try
        {
            QueryBox.Clear();
            PlayersGrid.ItemsSource = Array.Empty<OfflineExpandedPlayer>();
            PlayersGrid.SelectedIndex = -1;
            TechnicalGrid.ItemsSource = Array.Empty<OfflineAttribute>();
            MentalGrid.ItemsSource = Array.Empty<OfflineAttribute>();
            PhysicalGrid.ItemsSource = Array.Empty<OfflineAttribute>();
            PlayerText.Text = "Demonstração temporariamente indisponível.";
            ClubText.Text = "";
            CityCountryText.Text = "";
            ContractText.Text = "";
            WageText.Text = "";
            ClubMembersText.Text = "";
            StatusText.Text = "Falha na restauração offline. Use Mostrar todos para tentar novamente.";
            QueryBox.IsEnabled = false;
            SearchButton.IsEnabled = false;
            PlayersGrid.IsEnabled = false;
            PreviousButton.IsEnabled = false;
            NextButton.IsEnabled = false;
            ClubButton.IsEnabled = false;
            ResetButton.IsEnabled = true;
        }
        finally { _rendering = false; }
    }

    internal void SimulateRecoverableFailureForTests() =>
        RunSafely(() => throw new InvalidOperationException("Falha sintética de teste."));

    internal void SimulateResetFailureForTests(int failures)
    {
        if (failures is < 1 or > 2)
            throw new ArgumentOutOfRangeException(nameof(failures));
        _resetFailuresForTests = failures;
        RunSafely(() => throw new InvalidOperationException("Falha sintética antes da recuperação."));
    }

    // Strictly internal test-only fault injection; cannot be enabled by the public UI.
    // Unlike the finite counter, this remains active across any number of reset clicks.
    internal void StartPersistentResetFailureForTests()
    {
        _resetFailuresForTests = 0;
        _persistentResetFailureForTests = true;
        RunSafely(() => throw new InvalidOperationException("Synthetic persistent failure."));
    }

    internal void StopPersistentResetFailureForTests() =>
        _persistentResetFailureForTests = false;

    internal void SimulateResetClickFailureForTests()
    {
        _resetFailuresForTests = 1;
        Reset_Click(this, new RoutedEventArgs());
    }

    private void Search_Click(object sender, RoutedEventArgs e) =>
        RunSafely(() => _presenter.Search(QueryBox.Text));

    private void Reset_Click(object sender, RoutedEventArgs e) =>
        RunSafely(RestoreBaseline, isReset: true);

    private void Next_Click(object sender, RoutedEventArgs e) =>
        RunSafely(() => _presenter.NextPlayer());

    private void Previous_Click(object sender, RoutedEventArgs e) =>
        RunSafely(() => _presenter.PreviousPlayer());

    private void Club_Click(object sender, RoutedEventArgs e)
    {
        RunSafely(() =>
        {
            var clubId = _presenter.Current().SelectedClub?.DemonstrationId;
            var view = _presenter.ShowClub(clubId);
            // Only the invented ID is shown: no process, file path or external input.
            QueryBox.Text = view.SelectedClub?.DemonstrationId ?? "";
            return view;
        });
    }

    private void PlayersGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_rendering) return;
        RunSafely(() => _presenter.SelectPlayer(
            (PlayersGrid.SelectedItem as OfflineExpandedPlayer)?.Profile.DemonstrationId));
    }

    private void Render(OfflineWorkspaceState view)
    {
        _rendering = true;
        try
        {
            QueryBox.IsEnabled = true;
            SearchButton.IsEnabled = true;
            PlayersGrid.IsEnabled = true;
            ResetButton.IsEnabled = true;
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
