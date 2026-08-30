using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using MacMasterControlPro.Core.Services;

namespace MacMasterControlPro.Client.Pages;

public partial class NetworkPage : UserControl
{
    private readonly NetworkService _service = new();
    private readonly HashSet<string> _selected = new();

    public NetworkPage()
    {
        InitializeComponent();
        Rescan();
    }

    private void OnRescanClicked(object sender, RoutedEventArgs e) => Rescan();

    private void Rescan()
    {
        _service.ScanAdapters();
        _selected.Clear();
        Render();
    }

    private void OnSelectAllClicked(object sender, RoutedEventArgs e)
    {
        if (_selected.Count == _service.Adapters.Count)
        {
            _selected.Clear();
        }
        else
        {
            _selected.Clear();
            foreach (var adapter in _service.Adapters) _selected.Add(adapter);
        }
        Render();
    }

    private void Render()
    {
        AdaptersPanel.Children.Clear();
        foreach (var adapter in _service.Adapters)
        {
            var check = new CheckBox { Content = adapter, IsChecked = _selected.Contains(adapter), Margin = new Thickness(0, 2, 0, 2) };
            check.Checked += (_, _) => { _selected.Add(adapter); UpdateSelectionText(); };
            check.Unchecked += (_, _) => { _selected.Remove(adapter); UpdateSelectionText(); };
            AdaptersPanel.Children.Add(check);
        }
        UpdateSelectionText();
    }

    private void UpdateSelectionText()
    {
        SelectionText.Text = $"Selectate {_selected.Count} din {_service.Adapters.Count} plăci de rețea";
        SelectAllButton.Content = _selected.Count == _service.Adapters.Count && _service.Adapters.Count > 0 ? "Deselectează tot" : "Selectează tot";
        ApplyButton.IsEnabled = _selected.Count > 0;
    }

    private void OnApplyTuningClicked(object sender, RoutedEventArgs e)
    {
        if (!LicenseManager.Shared.IsUnlocked)
        {
            var gate = new TrialGateWindow { Owner = Window.GetWindow(this) };
            if (gate.ShowDialog() != true) return;
        }
        var ok = _service.ApplyTuning(_selected);
        StatusText.Text = ok ? "✔ Tuning aplicat." : "Anulat sau eșuat (UAC respins?).";
    }
}
