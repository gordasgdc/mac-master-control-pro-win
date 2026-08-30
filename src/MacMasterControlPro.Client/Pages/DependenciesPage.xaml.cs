using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using MacMasterControlPro.Core.Services;

namespace MacMasterControlPro.Client.Pages;

public partial class DependenciesPage : UserControl
{
    /// Ce se poate instala de-aici — `winget` insusi vine cu Windows, nu are
    /// sens un buton de "instalare" pentru el, doar status.
    private static readonly HashSet<string> Installable = new() { "rclone", "winfsp" };

    private readonly DependencyChecker _checker;
    private readonly MainWindow? _mainWindow;

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
        Render();
        _mainWindow?.RefreshDependencyBadge();
    }

    private void Render()
    {
        ItemsList.Items.Clear();
        foreach (var item in _checker.Items)
        {
            var row = new Grid { Margin = new Thickness(0, 4, 0, 4) };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var dot = new Ellipse
            {
                Width = 9, Height = 9, Margin = new Thickness(2, 0, 10, 0), VerticalAlignment = VerticalAlignment.Center,
                Fill = item.IsInstalled ? Brushes.LimeGreen : Brushes.OrangeRed,
            };
            Grid.SetColumn(dot, 0);
            row.Children.Add(dot);

            var info = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            info.Children.Add(new TextBlock { Text = item.Name, FontWeight = FontWeights.Bold });
            info.Children.Add(new TextBlock { Text = item.IsInstalled ? (item.Version ?? "Instalat") : "Neinstalat", Foreground = Brushes.Gray, FontSize = 11 });
            Grid.SetColumn(info, 1);
            row.Children.Add(info);

            // Buton propriu per componenta - rosu (neinstalat, apasabil) sau
            // verde (instalat, doar informativ) - cerinta explicita Cristi
            // 2026-08-30, inlocuieste selectia multipla folosita anterior
            // aici (un singur pachet instalat per click e comportamentul
            // firesc, nu un batch).
            if (Installable.Contains(item.Id))
            {
                var button = new Wpf.Ui.Controls.Button
                {
                    Content = item.IsInstalled ? "Instalat ✔" : "Instalează",
                    IsEnabled = !item.IsInstalled,
                    Background = new SolidColorBrush(item.IsInstalled ? Color.FromRgb(0x1F, 0x6B, 0x2E) : Color.FromRgb(0x8B, 0x22, 0x1A)),
                    Foreground = Brushes.White,
                };
                var id = item.Id;
                button.Click += (_, _) => OnInstallOneClicked(id);
                Grid.SetColumn(button, 2);
                row.Children.Add(button);
            }

            ItemsList.Items.Add(row);
        }
    }

    private void OnInstallOneClicked(string id)
    {
        Log.Clear();
        _checker.InstallSelected(new HashSet<string> { id }, line => Log.Append(line));
        Render();
        _mainWindow?.RefreshDependencyBadge();
    }
}
