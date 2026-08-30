using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using MacMasterControlPro.Core.Services;

namespace MacMasterControlPro.Client.Pages;

public partial class TweaksPage : UserControl
{
    private readonly TweaksService _service = new();

    public TweaksPage()
    {
        InitializeComponent();
        UpdateSelectionText();
    }

    private void OnSelectAllClicked(object sender, RoutedEventArgs e)
    {
        var allChecked = ExplorerCheck.IsChecked == true && ThumbsCheck.IsChecked == true;
        ExplorerCheck.IsChecked = !allChecked;
        ThumbsCheck.IsChecked = !allChecked;
    }

    private void OnCheckChanged(object sender, RoutedEventArgs e) => UpdateSelectionText();

    private void UpdateSelectionText()
    {
        var count = (ExplorerCheck.IsChecked == true ? 1 : 0) + (ThumbsCheck.IsChecked == true ? 1 : 0);
        SelectionText.Text = $"Selectat {count} din 2";
        SelectAllButton.Content = count == 2 ? "Deselectează tot" : "Selectează tot";
        ApplyButton.IsEnabled = count > 0;
    }

    private void OnApplyClicked(object sender, RoutedEventArgs e)
    {
        if (!RequireLicense()) return;
        var applied = 0;
        if (ExplorerCheck.IsChecked == true) { _service.EnableExplorerAdvancedView(); applied++; }
        if (ThumbsCheck.IsChecked == true) { _service.BlockThumbsDbOnNetworkDrives(); applied++; }
        StatusText.Text = $"✔ {applied} tweak-uri aplicate.";
        ExplorerCheck.IsChecked = false;
        ThumbsCheck.IsChecked = false;
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
