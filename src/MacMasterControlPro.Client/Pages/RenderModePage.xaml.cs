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
            StatusIndicator.Text = L.X("Mod Randare ACTIV", "Render Mode ON", "Modo de renderizado ACTIVO");
            StatusText.Text = L.X("Modul Randare era activ când aplicația s-a închis. Windows Search și File History pot fi încă oprite. Dezactivează comutatorul pentru a restaura exact starea de dinainte.", "Render Mode was active when the app closed. Windows Search and File History may still be paused. Turn the switch off to restore the exact previous state.", "El modo de renderizado estaba activo al cerrarse la app. Windows Search y el Historial de archivos pueden seguir pausados. Desactiva el interruptor para restaurar exactamente el estado anterior.");
        }
        else
        {
            Task.Run(() => _service.PreviewChanges()).ContinueWith(t =>
                Dispatcher.Invoke(() => { if (!_service.IsActive) StatusText.Text = L.X("Ce va schimba activarea acum:", "What turning it on will change:", "Qué cambiará al activarlo:") + "\n• " + string.Join("\n• ", t.Result); }),
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
        StatusIndicator.Text = _service.IsActive ? L.X("Mod Randare ACTIV", "Render Mode ON", "Modo de renderizado ACTIVO") : L.X("Mod Randare inactiv", "Render Mode off", "Modo de renderizado inactivo");
        StatusText.Text = _service.IsActive
            ? L.X("Nu uita să dezactivezi Modul Randare după export.", "Remember to turn Render Mode off after the export.", "No olvides desactivar el modo de renderizado tras exportar.")
            : "";
    }

    private bool RequireLicense()
    {
        if (LicenseManager.Shared.IsUnlocked) return true;
        var gate = new AccessGateWindow { Owner = Window.GetWindow(this) };
        return gate.ShowDialog() == true;
    }
}
