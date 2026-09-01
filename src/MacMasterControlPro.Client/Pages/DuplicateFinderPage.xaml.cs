using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using MacMasterControlPro.Core.Services;

namespace MacMasterControlPro.Client.Pages;

public partial class DuplicateFinderPage : UserControl
{
    private List<DuplicateGroup> _groups = new();
    private readonly HashSet<string> _marked = new();

    public DuplicateFinderPage()
    {
        InitializeComponent();
        RenderFolders();
    }

    private void RenderFolders()
    {
        var store = DuplicateScanFolders.Shared;
        FoldersPanel.Children.Clear();
        foreach (var path in store.Folders)
        {
            var row = new Grid { Margin = new Thickness(0, 2, 0, 2) };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var label = new TextBlock { Text = path, TextTrimming = TextTrimming.CharacterEllipsis, ToolTip = path };
            Grid.SetColumn(label, 0);
            row.Children.Add(label);
            var remove = new Wpf.Ui.Controls.Button { Content = "✕", Padding = new Thickness(6, 0, 6, 0) };
            remove.Click += (_, _) => { store.RemoveFolder(path); RenderFolders(); };
            Grid.SetColumn(remove, 1);
            row.Children.Add(remove);
            FoldersPanel.Children.Add(row);
        }
        if (store.Folders.Count == 0)
        {
            FoldersPanel.Children.Add(new TextBlock { Text = "Niciun folder ales — adaugă cel puțin unul.", Foreground = System.Windows.Media.Brushes.Orange, FontSize = 11 });
        }
        ScanButton.IsEnabled = store.Folders.Count > 0;
    }

    private void OnAddFolderClicked(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.OpenFolderDialog { Title = "Alege un folder de căutat" };
        if (dialog.ShowDialog() == true)
        {
            DuplicateScanFolders.Shared.AddFolder(dialog.FolderName);
            RenderFolders();
        }
    }

    private void OnScanClicked(object sender, RoutedEventArgs e)
    {
        var roots = DuplicateScanFolders.Shared.Folders;
        if (roots.Count == 0) return;
        ScanButton.IsEnabled = false;
        ScanStatusText.Text = "Se scanează…";
        GroupsPanel.Children.Clear();
        SummaryText.Text = "";
        DeleteRow.Visibility = Visibility.Collapsed;
        _marked.Clear();

        Task.Run(() =>
        {
            var result = DuplicateFinderService.Scan(roots, progress: line =>
                Dispatcher.Invoke(() => ScanStatusText.Text = line));
            Dispatcher.Invoke(() =>
            {
                _groups = result;
                ScanButton.IsEnabled = true;
                ScanStatusText.Text = result.Count == 0 ? "Niciun duplicat găsit." : "";
                // Sugestie implicita: pastreaza cel mai vechi ("originalul"
                // probabil), bifeaza restul spre stergere - userul poate
                // debifa/rebifa oricare inainte de a apasa Sterge.
                foreach (var group in result)
                {
                    var sorted = group.Files.OrderBy(f => f.ModifiedDate ?? DateTime.MaxValue).ToList();
                    foreach (var file in sorted.Skip(1)) _marked.Add(file.Path);
                }
                RenderGroups();
            });
        });
    }

    private void RenderGroups()
    {
        GroupsPanel.Children.Clear();
        if (_groups.Count > 0)
        {
            var reclaimable = _groups.Sum(g => g.ReclaimableBytes);
            SummaryText.Text = $"{_groups.Count} grupuri de duplicate găsite. Potențial recuperabil: {FormatBytes(reclaimable)}";
        }

        foreach (var group in _groups)
        {
            var card = new Wpf.Ui.Controls.CardExpander
            {
                Header = $"{group.Files.Count} copii identice · {FormatBytes(group.SizeBytes)} fiecare",
                IsExpanded = true,
                Margin = new Thickness(0, 0, 0, 8),
            };
            var stack = new StackPanel();
            foreach (var file in group.Files)
            {
                var row = new Grid { Margin = new Thickness(0, 3, 0, 3) };
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

                var infoStack = new StackPanel();
                var check = new CheckBox { Content = file.Path, IsChecked = _marked.Contains(file.Path), ToolTip = file.Path };
                check.Checked += (_, _) => { _marked.Add(file.Path); UpdateDeleteRow(); };
                check.Unchecked += (_, _) => { _marked.Remove(file.Path); UpdateDeleteRow(); };
                infoStack.Children.Add(check);
                if (file.ModifiedDate.HasValue)
                {
                    infoStack.Children.Add(new TextBlock { Text = file.ModifiedDate.Value.ToString("g"), FontSize = 10, Foreground = System.Windows.Media.Brushes.Gray, Margin = new Thickness(20, 0, 0, 0) });
                }
                Grid.SetColumn(infoStack, 0);
                row.Children.Add(infoStack);

                var openButton = new Wpf.Ui.Controls.Button
                {
                    Content = "📁",
                    Padding = new Thickness(6, 0, 6, 0),
                    ToolTip = "Deschide în Explorer — verifică fișierul înainte de a-l șterge.",
                };
                openButton.Click += (_, _) => OpenInExplorer(file.Path);
                Grid.SetColumn(openButton, 1);
                row.Children.Add(openButton);

                stack.Children.Add(row);
            }
            card.Content = stack;
            GroupsPanel.Children.Add(card);
        }
        UpdateDeleteRow();
    }

    private void UpdateDeleteRow()
    {
        DeleteRow.Visibility = _groups.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        var markedBytes = _groups.SelectMany(g => g.Files).Where(f => _marked.Contains(f.Path)).Sum(f => f.SizeBytes);
        MarkedText.Text = $"Bifate spre ștergere: {FormatBytes(markedBytes)}";
        DeleteMarkedButton.Content = $"Șterge fișierele bifate ({_marked.Count})";
        DeleteMarkedButton.IsEnabled = _marked.Count > 0;
    }

    private void OnDeleteMarkedClicked(object sender, RoutedEventArgs e)
    {
        if (!RequireLicense()) return;
        Log.Clear();
        var toDelete = _groups.SelectMany(g => g.Files).Where(f => _marked.Contains(f.Path)).ToList();
        DuplicateFinderService.Delete(toDelete, line => Log.Append(line));
        _marked.Clear();
        OnScanClicked(sender, e);
    }

    private static void OpenInExplorer(string path)
    {
        try { Process.Start("explorer.exe", $"/select,\"{path}\""); }
        catch { /* fisier posibil deja mutat/sters intre timp */ }
    }

    private static string FormatBytes(long bytes)
    {
        double b = bytes;
        string[] units = { "B", "KB", "MB", "GB" };
        int i = 0;
        while (b >= 1024 && i < units.Length - 1) { b /= 1024; i++; }
        return $"{b:0.#} {units[i]}";
    }

    private bool RequireLicense()
    {
        if (LicenseManager.Shared.IsUnlocked) return true;
        var gate = new TrialGateWindow { Owner = Window.GetWindow(this) };
        return gate.ShowDialog() == true;
    }
}
