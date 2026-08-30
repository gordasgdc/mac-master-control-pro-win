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
        var toDelete = _service.Items.Where(i => _selected.Contains(i.Id)).ToList();
        _service.DeleteSelected(toDelete);
        _selected.Clear();
        Render();
        StatusText.Text = "✔ Cache-uri șterse.";
    }

    private void OnPurgeClicked(object sender, RoutedEventArgs e)
    {
        if (!RequireLicense()) return;
        _service.PurgeRamAndFlushDns();
        StatusText.Text = "✔ RAM eliberat, DNS golit.";
    }

    private bool RequireLicense()
    {
        if (LicenseManager.Shared.IsUnlocked) return true;
        var gate = new TrialGateWindow { Owner = Window.GetWindow(this) };
        return gate.ShowDialog() == true;
    }
}
