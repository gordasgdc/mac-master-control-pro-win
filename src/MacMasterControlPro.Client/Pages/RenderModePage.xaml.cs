using System.Windows;
using System.Windows.Controls;
using MacMasterControlPro.Core.Services;

namespace MacMasterControlPro.Client.Pages;

public partial class RenderModePage : UserControl
{
    private readonly RenderModeService _service = new();
    private bool _suppressEvent;

    public RenderModePage()
    {
        InitializeComponent();
        _suppressEvent = true;
        RenderModeToggle.IsChecked = _service.IsActive;
        _suppressEvent = false;
    }

    private void OnToggled(object sender, RoutedEventArgs e)
    {
        if (_suppressEvent) return;
        if (!RequireLicense())
        {
            _suppressEvent = true;
            RenderModeToggle.IsChecked = _service.IsActive;
            _suppressEvent = false;
            return;
        }
        Log.Clear();
        if (RenderModeToggle.IsChecked == true)
        {
            _service.Activate(line => Log.Append(line));
        }
        else
        {
            _service.Deactivate(line => Log.Append(line));
        }
        StatusIndicator.Text = _service.IsActive ? "Mod Randare ACTIV" : "Mod Randare inactiv";
        StatusText.Text = _service.IsActive
            ? "Nu uita să dezactivezi Modul Randare după export."
            : "";
    }

    private bool RequireLicense()
    {
        if (LicenseManager.Shared.IsUnlocked) return true;
        var gate = new TrialGateWindow { Owner = Window.GetWindow(this) };
        return gate.ShowDialog() == true;
    }
}
