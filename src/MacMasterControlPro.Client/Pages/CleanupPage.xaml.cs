using System.Windows;
using System.Windows.Controls;
using MacMasterControlPro.Core.Services;

namespace MacMasterControlPro.Client.Pages;

public partial class CleanupPage : UserControl
{
    private readonly CleanupService _service = new();

    public CleanupPage() => InitializeComponent();

    private void OnAnalyzeClicked(object sender, RoutedEventArgs e) => ReportText.Text = _service.ScanReclaimable();

    private void OnCleanMediaClicked(object sender, RoutedEventArgs e)
    {
        if (!RequireLicense()) return;
        _service.CleanMediaCaches();
        StatusText.Text = "✔ Cache media curățat.";
    }

    private void OnPurgeClicked(object sender, RoutedEventArgs e)
    {
        if (!RequireLicense()) return;
        _service.PurgeRamAndFlushDns();
        StatusText.Text = "✔ RAM eliberat, DNS golit.";
    }

    private bool RequireLicense()
    {
        if (LicenseManager.Shared.IsUnlocked) return true;
        var gate = new TrialGateWindow { Owner = Window.GetWindow(this) };
        return gate.ShowDialog() == true;
    }
}
