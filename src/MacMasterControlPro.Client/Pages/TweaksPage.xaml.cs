using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using MacMasterControlPro.Core.Services;

namespace MacMasterControlPro.Client.Pages;

public partial class TweaksPage : UserControl
{
    private readonly TweaksService _service = new();
    private readonly HashSet<string> _spotlightSelected = new();
    private List<SpotlightTarget> _spotlightTargets = new();

    public TweaksPage()
    {
        InitializeComponent();
        UpdateSelectionText();
        RescanSpotlight();
    }

    // MARK: - Checklist Explorer (deja existent)

    private void OnSelectAllClicked(object sender, RoutedEventArgs e)
    {
        var allChecked = ExplorerCheck.IsChecked == true && ThumbsCheck.IsChecked == true;
        ExplorerCheck.IsChecked = !allChecked;
        ThumbsCheck.IsChecked = !allChecked;
    }

    private void OnCheckChanged(object sender, RoutedEventArgs e) => UpdateSelectionText();

    private void UpdateSelectionText()
    {
        var count = (ExplorerCheck.IsChecked == true ? 1 : 0) + (ThumbsCheck.IsChecked == true ? 1 : 0);
        SelectionText.Text = $"Selectat {count} din 2";
        SelectAllButton.Content = count == 2 ? "Deselectează tot" : "Selectează tot";
        ApplyButton.IsEnabled = count > 0;
    }

    private void OnApplyClicked(object sender, RoutedEventArgs e)
    {
        if (!RequireLicense()) return;
        var applied = 0;
        if (ExplorerCheck.IsChecked == true) { _service.EnableExplorerAdvancedView(); applied++; }
        if (ThumbsCheck.IsChecked == true) { _service.BlockThumbsDbOnNetworkDrives(); applied++; }
        StatusText.Text = $"✔ {applied} tweak-uri aplicate.";
        ExplorerCheck.IsChecked = false;
        ThumbsCheck.IsChecked = false;
    }

    // MARK: - Spotlight Shield (Manager Multi-Select, port 1:1 din Mac v2.3.0)

    private void RescanSpotlight()
    {
        _spotlightTargets = _service.ScanSpotlightTargets();
        _spotlightSelected.Clear();
        foreach (var target in _spotlightTargets.Where(t => _service.IsProtected(t.Path))) _spotlightSelected.Add(target.Path);
        RenderSpotlight();
    }

    private void OnSpotlightRescanClicked(object sender, RoutedEventArgs e) => RescanSpotlight();

    private void OnAddFoldersClicked(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFolderDialog { Multiselect = true };
        if (dialog.ShowDialog() != true) return;
        _service.AddCustomFolders(dialog.FolderNames);
        RescanSpotlight();
    }

    private void OnSpotlightSelectAllClicked(object sender, RoutedEventArgs e)
    {
        if (!RequireLicense()) return;
        var all = _spotlightSelected.Count == _spotlightTargets.Count;
        _spotlightSelected.Clear();
        if (!all) foreach (var target in _spotlightTargets) _spotlightSelected.Add(target.Path);
        _service.ApplyProtection(_spotlightTargets, _spotlightSelected);
        RenderSpotlight();
    }

    private void RenderSpotlight()
    {
        SpotlightItemsPanel.Children.Clear();
        if (_spotlightTargets.Count == 0)
        {
            SpotlightItemsPanel.Children.Add(new TextBlock
            {
                Text = "Niciun disc extern conectat și niciun folder adăugat.",
                FontSize = 11,
                Foreground = System.Windows.Media.Brushes.Gray,
            });
        }
        foreach (var target in _spotlightTargets)
        {
            var row = new Grid { Margin = new Thickness(0, 4, 0, 4) };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var check = new CheckBox { Content = target.Name, IsChecked = _spotlightSelected.Contains(target.Path) };
            check.Checked += (_, _) => { OnSpotlightToggled(target.Path, true); };
            check.Unchecked += (_, _) => { OnSpotlightToggled(target.Path, false); };
            Grid.SetColumn(check, 0);
            row.Children.Add(check);

            if (!target.IsVolume)
            {
                var remove = new Wpf.Ui.Controls.Button { Content = "✕", Padding = new Thickness(6, 0, 6, 0) };
                remove.Click += (_, _) => { _service.RemoveCustomFolder(target.Path); RescanSpotlight(); };
                Grid.SetColumn(remove, 1);
                row.Children.Add(remove);
            }

            SpotlightItemsPanel.Children.Add(row);
        }
        UpdateSpotlightSelectionText();
    }

    private void OnSpotlightToggled(string path, bool protect)
    {
        if (!RequireLicense())
        {
            RenderSpotlight();
            return;
        }
        _service.SetProtected(path, protect);
        if (protect) _spotlightSelected.Add(path); else _spotlightSelected.Remove(path);
        UpdateSpotlightSelectionText();
    }

    private void UpdateSpotlightSelectionText()
    {
        SpotlightSelectionText.Text = $"Protejate {_spotlightSelected.Count} din {_spotlightTargets.Count}";
        SpotlightSelectAllButton.Content = _spotlightSelected.Count == _spotlightTargets.Count && _spotlightTargets.Count > 0
            ? "Deselectează tot" : "Selectează tot";
    }

    private bool RequireLicense()
    {
        if (LicenseManager.Shared.IsUnlocked) return true;
        var gate = new TrialGateWindow { Owner = Window.GetWindow(this) };
        return gate.ShowDialog() == true;
    }
}
