using System.IO;
using System.Windows;
using System.Windows.Input;
using Microsoft.Win32;
using FMRTE2008.Application;
using FMRTE2008.Infrastructure.Windows;

namespace FMRTE2008.Desktop;

public partial class MainWindow : Window
{
    // Always creates an inert, in-memory source. No production process connector.
    private readonly IncrementalSearchController _search =
        new(() => new DemonstrationIndexedPlayerSource());
    private CancellationTokenSource? _scanCancellation;
    private bool _busy;
    private int _page = 1;
    private const int PageSize = 3;
    private const int ScanBatchSize = 3;

    public MainWindow()
    {
        InitializeComponent();
        _search.Restart("");
        RefreshPage();
        Loaded += async (_, _) => await ScanNextBatchAsync();
        Closed += (_, _) => _scanCancellation?.Cancel();
    }

    private void RefreshPage()
    {
        var matches = _search.Matches;
        int pages = Math.Max(1, (matches.Count + PageSize - 1) / PageSize);
        _page = Math.Clamp(_page, 1, pages);
        PlayersGrid.ItemsSource = matches.Skip((_page - 1) * PageSize).Take(PageSize).ToArray();
        PageText.Text = $"Demonstração: {matches.Count} encontrado(s) | Página {_page} de {pages}";
        PreviousButton.IsEnabled = !_busy && _page > 1;
        NextButton.IsEnabled = !_busy && _page < pages;
        LoadBatchButton.IsEnabled = !_busy && !_search.Complete && !_search.IsFailed;
        SearchButton.IsEnabled = !_busy;
        ClearButton.IsEnabled = !_busy;
        QueryBox.IsEnabled = !_busy;
        CancelScanButton.IsEnabled = _busy;
        ScanStatusText.Text = _search.IsFailed
            ? $"Sessão de demonstração encerrada: {_search.FailureReason}. Use Pesquisar para reiniciar."
            : $"Pesquisa incremental: {_search.NextIndex}/{_search.TotalCount} examinados" +
              $" | {_search.Failures.Count} registro(s) inválido(s)" +
              (_search.Complete ? " | Concluída." : " | Há mais registros.");
    }

    private async Task ScanNextBatchAsync()
    {
        if (_busy || _search.IsFailed || _search.Complete) return;
        _busy = true;
        _scanCancellation = new CancellationTokenSource();
        RefreshPage();
        try
        {
            // One bounded batch only; each click advances the cursor.
            await _search.NextAsync(ScanBatchSize, _scanCancellation.Token);
        }
        catch (OperationCanceledException)
        {
            // Controller marks the cursor unsafe; restart is explicit.
        }
        catch (Exception ex) when (ex is IOException or System.ComponentModel.Win32Exception
            or ObjectDisposedException or InvalidOperationException)
        {
            // No crash: controller reports a stopped session.
            ScanStatusText.Text = "Falha de leitura: " + ex.Message;
        }
        finally
        {
            _scanCancellation.Dispose();
            _scanCancellation = null;
            _busy = false;
            RefreshPage();
        }
    }

    private async Task RestartAndScanAsync(string query)
    {
        if (_busy) return;
        try
        {
            _search.Restart(query);
            _page = 1;
            RefreshPage();
            await ScanNextBatchAsync();
        }
        catch (Exception ex) when (ex is ArgumentException or UnauthorizedAccessException)
        {
            ScanStatusText.Text = "Não foi possível reiniciar a pesquisa: " + ex.Message;
        }
    }

    private async void SearchButton_Click(object sender, RoutedEventArgs e) =>
        await RestartAndScanAsync(QueryBox.Text);

    private async void ClearButton_Click(object sender, RoutedEventArgs e)
    {
        QueryBox.Clear();
        await RestartAndScanAsync("");
    }

    private async void QueryBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter) await RestartAndScanAsync(QueryBox.Text);
    }

    private async void LoadBatchButton_Click(object sender, RoutedEventArgs e) =>
        await ScanNextBatchAsync();

    private void CancelScanButton_Click(object sender, RoutedEventArgs e) =>
        _scanCancellation?.Cancel();

    private void PreviousButton_Click(object sender, RoutedEventArgs e)
    {
        _page = Math.Max(1, _page - 1);
        RefreshPage();
    }

    private void NextButton_Click(object sender, RoutedEventArgs e)
    {
        _page++;
        RefreshPage();
    }

    private void OpenOfflineProfiles_Click(object sender, RoutedEventArgs e) =>
        new OfflineProfilesWindow { Owner = this }.ShowDialog();

    private void SelectExeButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = "Selecione o executável do Football Manager 2008",
            Filter = "Executáveis Windows (*.exe)|*.exe",
            CheckFileExists = true,
            Multiselect = false
        };
        if (dialog.ShowDialog(this) != true) return;
        try
        {
            var identity = ExecutableFingerprint.InspectFile(dialog.FileName);
            var result = SupportedBuildCatalog.Recognize(identity);
            IdentityText.Text = $"Build do arquivo: {result.Variant} ({identity.FileVersion} / {identity.FilePrivatePart})";
            BuildStatusText.Text =
                $"Build do arquivo: {result.Variant} | Mapa operacional: NÃO APROVADO | Fonte: DEMONSTRAÇÃO";
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException
            or ArgumentException or System.ComponentModel.Win32Exception)
        {
            IdentityText.Text = "Falha ao identificar arquivo: " + ex.Message;
            BuildStatusText.Text = "Build-alvo: 8.0.2v1 | Mapa não aprovado | Fonte: demonstração";
        }
    }
}
