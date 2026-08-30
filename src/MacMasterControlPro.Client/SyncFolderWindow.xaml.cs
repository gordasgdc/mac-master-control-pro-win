using System.Collections.Generic;
using System.Windows;
using Microsoft.Win32;
using MacMasterControlPro.Core.Services;

namespace MacMasterControlPro.Client;

/// Sincronizare folder local &lt;-&gt; remote (Faza 4) - implicit "copy" (nu
/// sterge nimic), cu optiune explicita de oglinda exacta. Port 1:1 al
/// `SyncFolderSheet` (Mac).
public partial class SyncFolderWindow
{
    private readonly CloudManagerService _service;
    private readonly string _remoteName;
    private readonly string _remotePath;
    private string _localFolder = "";

    public List<string> ResultLog { get; } = new();

    public SyncFolderWindow(CloudManagerService service, string remoteName, string remotePath)
    {
        InitializeComponent();
        _service = service;
        _remoteName = remoteName;
        _remotePath = remotePath;
    }

    private void OnPickFolderClicked(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFolderDialog { Title = "Alege folderul local" };
        if (dialog.ShowDialog() != true) return;
        _localFolder = dialog.FolderName;
        FolderText.Text = _localFolder;
        FolderText.Foreground = System.Windows.Media.Brushes.LimeGreen;
        RunButton.IsEnabled = true;
    }

    private void OnMirrorChanged(object sender, RoutedEventArgs e)
    {
        MirrorWarning.Visibility = MirrorCheck.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
    }

    private void OnCancelClicked(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }

    private void OnRunClicked(object sender, RoutedEventArgs e)
    {
        RunButton.IsEnabled = false;
        RunButton.Content = "Se sincronizează…";
        Log.Clear();
        var direction = UploadRadio.IsChecked == true ? SyncDirection.Upload : SyncDirection.Download;
        _service.SyncFolder(_localFolder, _remoteName, _remotePath, direction, MirrorCheck.IsChecked == true,
            line => Dispatcher.Invoke(() => { Log.Append(line); ResultLog.Add(line); }),
            _ => Dispatcher.Invoke(() => { DialogResult = true; }));
    }
}
