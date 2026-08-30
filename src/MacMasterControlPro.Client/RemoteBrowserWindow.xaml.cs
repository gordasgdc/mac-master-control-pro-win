using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using MacMasterControlPro.Core.Services;

namespace MacMasterControlPro.Client;

/// Faza 3 - explorare rapida a unui remote FARA sa-l montezi, prin
/// `rclone lsjson`. Port 1:1 al `RemoteBrowserSheet.swift` (Mac).
public partial class RemoteBrowserWindow
{
    private readonly CloudManagerService _service;
    private readonly string _remoteName;
    private string _path = "";

    public RemoteBrowserWindow(CloudManagerService service, string remoteName)
    {
        InitializeComponent();
        _service = service;
        _remoteName = remoteName;
        TitleText.Text = $"Explorare — {remoteName}";
        _ = ReloadAsync();
    }

    private async void OnBackClicked(object sender, System.Windows.RoutedEventArgs e)
    {
        var parts = _path.Split('/', System.StringSplitOptions.RemoveEmptyEntries).ToList();
        if (parts.Count > 0) parts.RemoveAt(parts.Count - 1);
        _path = string.Join("/", parts);
        await ReloadAsync();
    }

    private async void OnEntryDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (EntriesList.SelectedItem is not RemoteEntry entry || !entry.IsDir) return;
        _path = entry.Path;
        await ReloadAsync();
    }

    private async Task ReloadAsync()
    {
        BreadcrumbText.Text = string.IsNullOrEmpty(_path) ? $"{_remoteName}:" : $"{_remoteName}:/{_path}";
        BackButton.Visibility = string.IsNullOrEmpty(_path) ? System.Windows.Visibility.Collapsed : System.Windows.Visibility.Visible;
        var path = _path;
        var entries = await Task.Run(() => _service.ListRemoteFolder(_remoteName, path));
        EntriesList.ItemsSource = entries;
    }
}
