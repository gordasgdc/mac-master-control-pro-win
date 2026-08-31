using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using MacMasterControlPro.Core.Services;

namespace MacMasterControlPro.Client.Pages;

public partial class CleanupPage : UserControl
{
    private readonly CleanupService _service = new();
    private readonly HashSet<string> _selected = new();
    private List<BigFileWin> _bigFiles = new();
    private readonly HashSet<string> _selectedBigFiles = new();

    public CleanupPage()
    {
        InitializeComponent();
        Rescan();
    }

    private void OnRescanClicked(object sender, RoutedEventArgs e) => Rescan();

    private void Rescan()
    {
        _service.ScanReclaimable();
        _selected.Clear();
        Render();
    }

    private void OnSelectAllClicked(object sender, RoutedEventArgs e)
    {
        if (_selected.Count == _service.Items.Count)
        {
            _selected.Clear();
        }
        else
        {
            _selected.Clear();
            foreach (var item in _service.Items) _selected.Add(item.Id);
        }
        Render();
    }

    private void Render()
    {
        ItemsPanel.Children.Clear();
        foreach (var item in _service.Items)
        {
            var row = new Grid { Margin = new Thickness(0, 4, 0, 4) };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var check = new CheckBox { Content = item.Name, IsChecked = _selected.Contains(item.Id) };
            check.Checked += (_, _) => { _selected.Add(item.Id); UpdateSelectionText(); };
            check.Unchecked += (_, _) => { _selected.Remove(item.Id); UpdateSelectionText(); };
            Grid.SetColumn(check, 0);
            row.Children.Add(check);

            var size = new TextBlock { Text = $"{item.SizeGB:F2} GB", Foreground = System.Windows.Media.Brushes.Gray, FontSize = 11, VerticalAlignment = VerticalAlignment.Center };
            Grid.SetColumn(size, 1);
            row.Children.Add(size);

            ItemsPanel.Children.Add(row);
        }
        UpdateSelectionText();
    }

    private void UpdateSelectionText()
    {
        var selectedBytes = _service.Items.Where(i => _selected.Contains(i.Id)).Sum(i => i.SizeBytes);
        var totalBytes = _service.Items.Sum(i => i.SizeBytes);
        SelectionText.Text = $"Selectat {selectedBytes / 1024.0 / 1024.0 / 1024.0:F2} GB din {totalBytes / 1024.0 / 1024.0 / 1024.0:F2} GB";
        SelectAllButton.Content = _selected.Count == _service.Items.Count && _service.Items.Count > 0 ? "Deselectează tot" : "Selectează tot";
        DeleteButton.IsEnabled = _selected.Count > 0;
    }

    private void OnDeleteClicked(object sender, RoutedEventArgs e)
    {
        if (!RequireLicense()) return;
        Log.Clear();
        Log.Append("$ Ștergere cache-uri selectate…");
        DeleteButton.IsEnabled = false;
        var toDelete = _service.Items.Where(i => _selected.Contains(i.Id)).ToList();
        _service.DeleteSelected(toDelete, line => Log.Append(line));
        _selected.Clear();
        Render();
        StatusText.Text = "✔ Gata — vezi detaliile mai jos.";
        DeleteButton.IsEnabled = true;
    }

    private void OnPurgeClicked(object sender, RoutedEventArgs e)
    {
        if (!RequireLicense()) return;
        Log.Clear();
        Log.Append("$ Purjare RAM + flush DNS…");
        _service.PurgeRamAndFlushDns();
        Log.Append("✔ RAM eliberat pe toate procesele accesibile, cache DNS golit.");
        StatusText.Text = "✔ RAM eliberat, DNS golit.";
    }

    private void OnScanBigFilesClicked(object sender, RoutedEventArgs e)
    {
        _bigFiles = BigFileFinderService.Scan();
        _selectedBigFiles.Clear();
        RenderBigFiles();
    }

    private void RenderBigFiles()
    {
        BigFilesPanel.Children.Clear();
        foreach (var file in _bigFiles)
        {
            var row = new Grid { Margin = new Thickness(0, 3, 0, 3) };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var check = new CheckBox
            {
                Content = file.Name,
                IsChecked = _selectedBigFiles.Contains(file.Path),
                ToolTip = file.Path,
            };
            check.Checked += (_, _) => { _selectedBigFiles.Add(file.Path); DeleteBigFilesButton.IsEnabled = true; };
            check.Unchecked += (_, _) => { _selectedBigFiles.Remove(file.Path); DeleteBigFilesButton.IsEnabled = _selectedBigFiles.Count > 0; };
            Grid.SetColumn(check, 0);
            row.Children.Add(check);

            var size = new TextBlock { Text = file.SizeDescription, Foreground = System.Windows.Media.Brushes.Gray, FontSize = 11 };
            Grid.SetColumn(size, 1);
            row.Children.Add(size);

            BigFilesPanel.Children.Add(row);
        }
    }

    private void OnDeleteBigFilesClicked(object sender, RoutedEventArgs e)
    {
        if (!RequireLicense()) return;
        Log.Clear();
        var toDelete = _bigFiles.Where(f => _selectedBigFiles.Contains(f.Path)).ToList();
        BigFileFinderService.Delete(toDelete, line => Log.Append(line));
        _bigFiles = BigFileFinderService.Scan();
        _selectedBigFiles.Clear();
        RenderBigFiles();
        DeleteBigFilesButton.IsEnabled = false;
    }

    private void OnEmptyRecycleBinClicked(object sender, RoutedEventArgs e)
    {
        if (!RequireLicense()) return;
        Log.Clear();
        Log.Append("$ Golesc Coșul de reciclare…");
        var ok = _service.EmptyRecycleBin();
        Log.Append(ok ? "✔ Coș de reciclare golit." : "EROARE la golirea Coșului de reciclare.");
        StatusText.Text = ok ? "✔ Coș de reciclare golit." : "EROARE.";
    }

    private bool RequireLicense()
    {
        if (LicenseManager.Shared.IsUnlocked) return true;
        var gate = new TrialGateWindow { Owner = Window.GetWindow(this) };
        return gate.ShowDialog() == true;
    }
}
