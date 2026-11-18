using System.Windows;
using Microsoft.Win32;
using FMRTE2008.Application;
using FMRTE2008.Infrastructure.Windows;

namespace FMRTE2008.Desktop;

public partial class MainWindow : Window
{
    public MainWindow() => InitializeComponent();

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
            IdentityText.Text =
                $"Versão: {identity.FileVersion}\n" +
                $"FilePrivatePart: {identity.FilePrivatePart}\n" +
                $"SHA-256: {identity.Sha256}\n" +
                $"Variante: {result.Variant}\n" +
                $"Estado: {result.Reason}";
        }
        catch (Exception ex) when (
            ex is IOException or UnauthorizedAccessException or
            ArgumentException or System.ComponentModel.Win32Exception)
        {
            IdentityText.Text = "Não foi possível identificar o arquivo: " + ex.Message;
        }
    }
}
