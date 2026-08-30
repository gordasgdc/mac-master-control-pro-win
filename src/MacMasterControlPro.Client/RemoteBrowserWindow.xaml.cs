using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using Microsoft.Win32;
using MacMasterControlPro.Core.Services;
using Wpf.Ui.Controls;

namespace MacMasterControlPro.Client;

/// Faza 3+4 - explorare rapida a unui remote FARA sa-l montezi, plus
/// operatiile reale de management de fisiere (upload/download/stergere) -
/// cerinta explicita 2026-08-30. Port 1:1 al `RemoteBrowserSheet.swift` (Mac).
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

    private async void OnBackClicked(object sender, RoutedEventArgs e)
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

    private void OnSelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        var hasSelection = EntriesList.SelectedItem is RemoteEntry;
        DownloadButton.IsEnabled = hasSelection;
        DeleteButton.IsEnabled = hasSelection;
    }

    private async Task ReloadAsync()
    {
        BreadcrumbText.Text = string.IsNullOrEmpty(_path) ? $"{_remoteName}:" : $"{_remoteName}:/{_path}";
        BackButton.Visibility = string.IsNullOrEmpty(_path) ? Visibility.Collapsed : Visibility.Visible;
        var path = _path;
        var entries = await Task.Run(() => _service.ListRemoteFolder(_remoteName, path));
        EntriesList.ItemsSource = entries;
        DownloadButton.IsEnabled = false;
        DeleteButton.IsEnabled = false;
    }

    private void OnUploadClicked(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog { Title = "Alege fișiere de încărcat", Multiselect = true };
        if (dialog.ShowDialog() != true) return;
        Log.Clear();
        _service.Upload(_remoteName, _path, dialog.FileNames, line => Dispatcher.Invoke(() => Log.Append(line)), _ => Dispatcher.Invoke(async () => await ReloadAsync()));
    }

    private void OnDownloadClicked(object sender, RoutedEventArgs e)
    {
        if (EntriesList.SelectedItem is not RemoteEntry entry) return;
        var dialog = new OpenFolderDialog { Title = "Alege unde se descarcă" };
        if (dialog.ShowDialog() != true) return;
        Log.Clear();
        _service.Download(_remoteName, entry.Path, dialog.FolderName, line => Dispatcher.Invoke(() => Log.Append(line)), _ => { });
    }

    private void OnDeleteClicked(object sender, RoutedEventArgs e)
    {
        if (EntriesList.SelectedItem is not RemoteEntry entry) return;
        var confirm = new Wpf.Ui.Controls.MessageBox
        {
            Title = $"Ștergi „{entry.Name}”?",
            Content = $"Această acțiune e ireversibilă — fișierul/folderul se șterge direct de pe {_remoteName}.",
            PrimaryButtonText = "Șterge definitiv",
            CloseButtonText = "Anulează",
        };
        confirm.ShowDialogAsync().ContinueWith(t =>
        {
            if (t.Result != Wpf.Ui.Controls.MessageBoxResult.Primary) return;
            Dispatcher.Invoke(() =>
            {
                Log.Clear();
                _service.DeleteRemoteEntry(_remoteName, entry.Path, entry.IsDir, line => Dispatcher.Invoke(() => Log.Append(line)));
                _ = ReloadAsync();
            });
        });
    }

    private void OnSyncClicked(object sender, RoutedEventArgs e)
    {
        var window = new SyncFolderWindow(_service, _remoteName, _path) { Owner = this };
        if (window.ShowDialog() == true)
        {
            foreach (var line in window.ResultLog) Log.Append(line);
        }
    }
}
