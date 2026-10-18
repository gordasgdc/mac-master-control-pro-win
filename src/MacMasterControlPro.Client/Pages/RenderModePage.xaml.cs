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
        if (_service.RecoveredFromPreviousSession)
        {
            StatusIndicator.Text = "Mod Randare ACTIV";
            StatusText.Text = "Modul Randare era activ când aplicația s-a închis. Windows Search și File History pot fi încă oprite. Dezactivează comutatorul pentru a restaura exact starea de dinainte.";
        }
        else
        {
            Task.Run(() => _service.PreviewChanges()).ContinueWith(t =>
                Dispatcher.Invoke(() => { if (!_service.IsActive) StatusText.Text = "Ce va schimba activarea acum:\n• " + string.Join("\n• ", t.Result); }),
                TaskContinuationOptions.OnlyOnRanToCompletion);
        }
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
