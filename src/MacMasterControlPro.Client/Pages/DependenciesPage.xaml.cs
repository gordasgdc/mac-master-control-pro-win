using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using MacMasterControlPro.Core.Services;

namespace MacMasterControlPro.Client.Pages;

public partial class DependenciesPage : UserControl
{
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

    private void OnInstallClicked(object sender, RoutedEventArgs e)
    {
        InstallButton.IsEnabled = false;
        LogText.Text = "Se instalează…";
        LogText.Text = _checker.InstallMissing();
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
            row.Children.Add(new Ellipse
            {
                Width = 9, Height = 9, Margin = new Thickness(0, 0, 8, 0),
                Fill = item.IsInstalled ? Brushes.LimeGreen : Brushes.OrangeRed,
            });
            row.Children.Add(new TextBlock { Text = item.Name, FontWeight = FontWeights.Bold, Width = 200 });
            row.Children.Add(new TextBlock { Text = item.IsInstalled ? (item.Version ?? "Instalat") : "Neinstalat", Foreground = Brushes.Gray, FontSize = 11 });
            ItemsList.Items.Add(row);
        }
        InstallButton.Visibility = _checker.AllInstalled ? Visibility.Collapsed : Visibility.Visible;
    }
}
