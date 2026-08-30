using System.Windows;
using System.Windows.Controls;
using MacMasterControlPro.Core.Services;

namespace MacMasterControlPro.Client.Pages;

public partial class NetworkPage : UserControl
{
    private readonly NetworkService _service = new();

    public NetworkPage()
    {
        InitializeComponent();
        Rescan();
    }

    private void OnRescanClicked(object sender, RoutedEventArgs e) => Rescan();

    private void Rescan()
    {
        _service.ScanAdapters();
        AdapterCombo.ItemsSource = _service.Adapters;
        if (_service.Adapters.Count > 0) AdapterCombo.SelectedIndex = 0;
    }

    private void OnApplyTuningClicked(object sender, RoutedEventArgs e)
    {
        if (!LicenseManager.Shared.IsUnlocked)
        {
            var gate = new TrialGateWindow { Owner = Window.GetWindow(this) };
            if (gate.ShowDialog() != true) return;
        }
        _service.SelectedAdapter = AdapterCombo.SelectedItem as string ?? "";
        var ok = _service.ApplyTuning();
        StatusText.Text = ok ? "✔ Tuning aplicat." : "Anulat sau eșuat (UAC respins?).";
    }
}
