using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using MacMasterControlPro.Core.Services;

namespace MacMasterControlPro.Client.Pages;

public partial class TweaksPage : UserControl
{
    private readonly TweaksService _service = new();

    public TweaksPage() => InitializeComponent();

    private void OnExplorerClicked(object sender, RoutedEventArgs e)
    {
        if (!RequireLicense()) return;
        _service.EnableExplorerAdvancedView();
        StatusText.Text = "✔ Vizualizare avansată Explorer aplicată.";
    }

    private void OnThumbsClicked(object sender, RoutedEventArgs e)
    {
        if (!RequireLicense()) return;
        _service.BlockThumbsDbOnNetworkDrives();
        StatusText.Text = "✔ thumbs.db blocat pe unități de rețea.";
    }

    private void OnProtectFolderClicked(object sender, RoutedEventArgs e)
    {
        if (!RequireLicense()) return;
        var dialog = new OpenFolderDialog();
        if (dialog.ShowDialog() != true) return;
        var ok = _service.ProtectFromIndexing(dialog.FolderName);
        ProtectStatusText.Text = ok ? $"✔ Protejat: {dialog.FolderName}" : "Eroare — cale invalidă.";
    }

    private bool RequireLicense()
    {
        if (LicenseManager.Shared.IsUnlocked) return true;
        var gate = new TrialGateWindow { Owner = Window.GetWindow(this) };
        return gate.ShowDialog() == true;
    }
}
