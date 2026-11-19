using System.Windows;
using System.Windows.Input;
using Microsoft.Win32;
using FMRTE2008.Application;
using FMRTE2008.Infrastructure.Windows;

namespace FMRTE2008.Desktop;

public partial class MainWindow : Window
{
    private readonly PlayerQueryService _queryService = new(new DemonstrationPlayerSource());
    private int _page = 1;
    private const int PageSize = 3;

    public MainWindow()
    {
        InitializeComponent();
        RefreshPage();
    }

    private void RefreshPage()
    {
        var result = _queryService.Search(QueryBox.Text, _page, PageSize);
        _page = result.PageNumber;
        PlayersGrid.ItemsSource = result.Items;
        PageText.Text = $"Demonstração: {result.TotalMatches} registro(s) | Página {_page} de {result.PageCount} | {PageSize} por página";
        PreviousButton.IsEnabled = _page > 1;
        NextButton.IsEnabled = _page < result.PageCount;
    }

    private void SearchButton_Click(object sender, RoutedEventArgs e)
    {
        _page = 1;
        RefreshPage();
    }

    private void ClearButton_Click(object sender, RoutedEventArgs e)
    {
        QueryBox.Clear();
        _page = 1;
        RefreshPage();
    }

    private void QueryBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            _page = 1;
            RefreshPage();
        }
    }

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
                $"Build do arquivo: {result.Variant} | Mapa operacional: NÃO APROVADO | Fonte de jogadores: DEMONSTRAÇÃO";
            // Identificar arquivo não habilita a leitura do processo.
        }
        catch (Exception ex) when (ex is System.IO.IOException or UnauthorizedAccessException
            or ArgumentException or System.ComponentModel.Win32Exception)
        {
            IdentityText.Text = "Falha ao identificar arquivo: " + ex.Message;
            BuildStatusText.Text = "Build-alvo: 8.0.2v1 | Mapa não aprovado | Fonte: demonstração";
        }
    }
}
