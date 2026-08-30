using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using Microsoft.Win32;
using MacMasterControlPro.Core.Services;
using Wpf.Ui.Controls;

namespace MacMasterControlPro.Client.Pages;

public partial class CloudPage : UserControl
{
    private readonly CloudManagerService _service = new();
    private readonly HashSet<string> _selected = new();
    /// Faza 2: TextBlock-ul de statistici live per remote montat, actualizat
    /// de timer fara sa reconstruiasca toata lista la fiecare 2 secunde.
    private readonly Dictionary<string, System.Windows.Controls.TextBlock> _statsTextBlocks = new();
    private readonly DispatcherTimer _statsTimer;

    public CloudPage()
    {
        InitializeComponent();
        RenderMountFolderText();
        Refresh();

        _statsTimer = new DispatcherTimer { Interval = System.TimeSpan.FromSeconds(2) };
        _statsTimer.Tick += (_, _) => RefreshStats();
        _statsTimer.Start();
        Unloaded += (_, _) => _statsTimer.Stop();
    }

    private void RefreshStats()
    {
        foreach (var (remoteName, textBlock) in _statsTextBlocks)
        {
            var stats = _service.FetchStats(remoteName);
            textBlock.Text = stats is null
                ? ""
                : $"↕︎ {FormatBytes(stats.SpeedBytesPerSec)}/s · {FormatBytes(stats.BytesTransferred)} transferați · {stats.ActiveTransfers} active";
        }
    }

    private static string FormatBytes(double bytes)
    {
        string[] units = { "B", "KB", "MB", "GB", "TB" };
        var unit = 0;
        while (bytes >= 1024 && unit < units.Length - 1) { bytes /= 1024; unit++; }
        return $"{bytes:0.#} {units[unit]}";
    }

    private void OnRescanClicked(object sender, RoutedEventArgs e) => Refresh();

    private void OnAddClicked(object sender, RoutedEventArgs e)
    {
        var window = new AddCloudRemoteWindow(_service) { Owner = Window.GetWindow(this) };
        if (window.ShowDialog() == true) Refresh();
    }

    private void OnChooseMountFolderClicked(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFolderDialog { Title = "Alege folderul unde se montează conturile Cloud" };
        if (dialog.ShowDialog() != true) return;
        CloudMountSettings.CustomMountFolder = dialog.FolderName;
        RenderMountFolderText();
    }

    private void OnResetMountFolderClicked(object sender, RoutedEventArgs e)
    {
        CloudMountSettings.CustomMountFolder = null;
        RenderMountFolderText();
    }

    private void RenderMountFolderText()
    {
        var folder = CloudMountSettings.CustomMountFolder;
        MountFolderText.Text = string.IsNullOrWhiteSpace(folder)
            ? "Implicit: literă de disc nouă per cont."
            : $"Folder curent: {folder}";
        MountFolderText.Foreground = string.IsNullOrWhiteSpace(folder) ? System.Windows.Media.Brushes.Gray : System.Windows.Media.Brushes.LimeGreen;
    }

    private void OnSelectAllClicked(object sender, RoutedEventArgs e)
    {
        if (_selected.Count == _service.Remotes.Count)
        {
            _selected.Clear();
        }
        else
        {
            _selected.Clear();
            foreach (var remote in _service.Remotes) _selected.Add(remote.Name);
        }
        Refresh();
    }

    private void OnMountSelectedClicked(object sender, RoutedEventArgs e)
    {
        if (!LicenseManager.Shared.IsUnlocked)
        {
            var gate = new TrialGateWindow { Owner = Window.GetWindow(this) };
            if (gate.ShowDialog() != true) return;
        }
        Log.Clear();
        foreach (var name in _selected.Where(n => !_service.MountedDriveLetters.ContainsKey(n)))
        {
            _service.Mount(name, line => Log.Append(line));
        }
        Refresh();
    }

    private void OnUnmountSelectedClicked(object sender, RoutedEventArgs e)
    {
        Log.Clear();
        foreach (var name in _selected.Where(n => _service.MountedDriveLetters.ContainsKey(n)))
        {
            _service.Unmount(name, line => Log.Append(line));
        }
        Refresh();
    }

    private void Refresh()
    {
        _service.RefreshRemotes();
        RemotesList.Items.Clear();
        _statsTextBlocks.Clear();
        EmptyText.Visibility = _service.Remotes.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

        foreach (var remote in _service.Remotes)
        {
            var container = new StackPanel { Margin = new Thickness(0, 6, 0, 6) };
            var row = new Grid();
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); // 0: checkbox
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }); // 1: nume+tip

            void AddButton(System.Windows.FrameworkElement element)
            {
                var col = row.ColumnDefinitions.Count;
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                Grid.SetColumn(element, col);
                row.Children.Add(element);
            }

            var check = new CheckBox { IsChecked = _selected.Contains(remote.Name), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 8, 0) };
            check.Checked += (_, _) => { _selected.Add(remote.Name); UpdateSelectionText(); };
            check.Unchecked += (_, _) => { _selected.Remove(remote.Name); UpdateSelectionText(); };
            Grid.SetColumn(check, 0);
            row.Children.Add(check);

            var infoStack = new StackPanel();
            infoStack.Children.Add(new System.Windows.Controls.TextBlock { Text = remote.Name, FontWeight = FontWeights.Bold });
            infoStack.Children.Add(new System.Windows.Controls.TextBlock { Text = remote.Type, Foreground = System.Windows.Media.Brushes.Gray, FontSize = 11 });
            Grid.SetColumn(infoStack, 1);
            row.Children.Add(infoStack);

            var isMounted = _service.MountedDriveLetters.ContainsKey(remote.Name);

            if (isMounted)
            {
                var openButton = new Wpf.Ui.Controls.Button { Content = "Deschide", Margin = new Thickness(0, 0, 8, 0) };
                openButton.Click += (_, _) => System.Diagnostics.Process.Start("explorer.exe", _service.MountedDriveLetters[remote.Name]);
                AddButton(openButton);
            }

            var browseButton = new Wpf.Ui.Controls.Button { Content = "Explorează", Margin = new Thickness(0, 0, 8, 0) };
            browseButton.Click += (_, _) => new RemoteBrowserWindow(_service, remote.Name) { Owner = Window.GetWindow(this) }.ShowDialog();
            AddButton(browseButton);

            var actionButton = new Wpf.Ui.Controls.Button
            {
                Content = isMounted ? $"Demontează ({_service.MountedDriveLetters[remote.Name]})" : "Montează",
                Margin = new Thickness(0, 0, 8, 0),
            };
            actionButton.Click += (_, _) =>
            {
                Log.Clear();
                if (isMounted) { _service.Unmount(remote.Name, line => Log.Append(line)); Refresh(); return; }
                if (!LicenseManager.Shared.IsUnlocked)
                {
                    var gate = new TrialGateWindow { Owner = Window.GetWindow(this) };
                    if (gate.ShowDialog() != true) return;
                }
                var target = _service.Mount(remote.Name, line => Log.Append(line));
                StatusText.Text = target is not null ? $"✔ Montat pe {target}" : "Nicio literă de disc liberă.";
                Refresh();
            };
            AddButton(actionButton);

            var deleteButton = new Wpf.Ui.Controls.Button { Content = "🗑" };
            deleteButton.Click += (_, _) => { _service.DeleteRemote(remote.Name); Refresh(); };
            AddButton(deleteButton);

            container.Children.Add(row);

            if (isMounted)
            {
                var statsText = new System.Windows.Controls.TextBlock
                {
                    FontFamily = new System.Windows.Media.FontFamily("Consolas"),
                    FontSize = 10.5,
                    Foreground = System.Windows.Media.Brushes.Gray,
                    Margin = new Thickness(0, 2, 0, 0),
                };
                _statsTextBlocks[remote.Name] = statsText;
                container.Children.Add(statsText);
            }

            RemotesList.Items.Add(container);
        }
        UpdateSelectionText();
        RefreshStats();
    }

    private void UpdateSelectionText()
    {
        SelectionText.Text = $"Selectate {_selected.Count} din {_service.Remotes.Count}";
        SelectAllButton.Content = _selected.Count == _service.Remotes.Count && _service.Remotes.Count > 0 ? "Deselectează tot" : "Selectează tot";
        MountSelectedButton.IsEnabled = _selected.Count > 0;
        UnmountSelectedButton.IsEnabled = _selected.Count > 0;
    }
}
