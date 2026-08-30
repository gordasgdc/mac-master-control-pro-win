using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using MacMasterControlPro.Core.Services;

namespace MacMasterControlPro.Client.Pages;

public partial class DependenciesPage : UserControl
{
    /// Ce se poate instala de-aici — `winget` insusi vine cu Windows, nu are
    /// sens o bifa de "instalare" pentru el, doar status.
    private static readonly HashSet<string> Installable = new() { "rclone", "winfsp" };

    private readonly DependencyChecker _checker;
    private readonly MainWindow? _mainWindow;
    private readonly HashSet<string> _selected = new();

    public DependenciesPage(DependencyChecker checker, MainWindow? mainWindow = null)
    {
        InitializeComponent();
        _checker = checker;
        _mainWindow = mainWindow;
        Render();
    }

    private void OnRescanClicked(object sender, RoutedEventArgs e)
    {
        _checker.CheckAll();
        _selected.RemoveWhere(id => _checker.Items.FirstOrDefault(i => i.Id == id)?.IsInstalled == true);
        Render();
        _mainWindow?.RefreshDependencyBadge();
    }

    private void OnInstallClicked(object sender, RoutedEventArgs e)
    {
        if (_selected.Count == 0) return;
        InstallButton.IsEnabled = false;
        LogText.Text = "Se instalează…";
        LogText.Text = _checker.InstallSelected(_selected);
        _selected.Clear();
        Render();
        _mainWindow?.RefreshDependencyBadge();
        InstallButton.IsEnabled = true;
    }

    private void Render()
    {
        ItemsList.Items.Clear();
        foreach (var item in _checker.Items)
        {
            var row = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 4, 0, 4) };

            // Punctul de status (verde/rosu) ramane mereu vizibil, indiferent
            // de bifa - fara el, un checkbox needit+dezactivat pentru un
            // pachet deja instalat nu comunica vizual "e OK" (bug real,
            // gasit 2026-08-30: "de ce nu apare cu verde?").
            row.Children.Add(new Ellipse
            {
                Width = 9, Height = 9, Margin = new Thickness(2, 0, 10, 0),
                Fill = item.IsInstalled ? Brushes.LimeGreen : Brushes.OrangeRed,
            });

            if (Installable.Contains(item.Id) && !item.IsInstalled)
            {
                var check = new CheckBox
                {
                    IsChecked = _selected.Contains(item.Id),
                    Margin = new Thickness(0, 0, 8, 0),
                    VerticalAlignment = VerticalAlignment.Center,
                };
                check.Checked += (_, _) => { _selected.Add(item.Id); UpdateSelectionText(); };
                check.Unchecked += (_, _) => { _selected.Remove(item.Id); UpdateSelectionText(); };
                row.Children.Add(check);
            }

            row.Children.Add(new TextBlock { Text = item.Name, FontWeight = FontWeights.Bold, Width = 190 });
            row.Children.Add(new TextBlock { Text = item.IsInstalled ? (item.Version ?? "Instalat") : "Neinstalat", Foreground = Brushes.Gray, FontSize = 11 });
            ItemsList.Items.Add(row);
        }
        UpdateSelectionText();
    }

    private void UpdateSelectionText()
    {
        var installableCount = _checker.Items.Count(i => Installable.Contains(i.Id) && !i.IsInstalled);
        SelectionText.Text = installableCount == 0
            ? "Toate componentele instalabile de-aici sunt deja prezente."
            : $"Selectat {_selected.Count} din {installableCount} componente neinstalate.";
        InstallButton.IsEnabled = _selected.Count > 0;
        InstallButton.Visibility = installableCount == 0 ? Visibility.Collapsed : Visibility.Visible;
    }
}
