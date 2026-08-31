using System.Windows;
using System.Windows.Controls;
using MacMasterControlPro.Core.Services;

namespace MacMasterControlPro.Client.Pages;

public partial class LoginItemsPage : UserControl
{
    private List<LoginItem> _items = new();

    public LoginItemsPage()
    {
        InitializeComponent();
        Rescan();
    }

    private void OnRescanClicked(object sender, RoutedEventArgs e) => Rescan();

    private void Rescan()
    {
        _items = LoginItemsService.Scan();
        Render();
    }

    private void Render()
    {
        var disabled = LoginItemsService.DisabledNames();
        ItemsPanel.Children.Clear();
        if (_items.Count == 0)
        {
            ItemsPanel.Children.Add(new TextBlock { Text = "Niciun serviciu de fundal terț găsit.", Foreground = System.Windows.Media.Brushes.Gray, Margin = new Thickness(0, 12, 0, 12) });
            return;
        }
        foreach (var item in _items)
        {
            var row = new Grid { Margin = new Thickness(0, 4, 0, 4) };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var labelPanel = new StackPanel();
            labelPanel.Children.Add(new TextBlock { Text = item.Name, FontFamily = new System.Windows.Media.FontFamily("Consolas") });
            labelPanel.Children.Add(new TextBlock { Text = item.IsHKLM ? "Sistem (necesită admin)" : "Utilizator", FontSize = 10, Foreground = System.Windows.Media.Brushes.Gray });
            Grid.SetColumn(labelPanel, 0);
            row.Children.Add(labelPanel);

            var isDisabled = disabled.Contains(item.Name);
            var button = new Wpf.Ui.Controls.Button
            {
                Content = isDisabled ? "Reactivează" : "Dezactivează",
                Foreground = isDisabled ? System.Windows.Media.Brushes.LightGreen : System.Windows.Media.Brushes.OrangeRed,
            };
            button.Click += (_, _) => Toggle(item, enable: isDisabled);
            Grid.SetColumn(button, 1);
            row.Children.Add(button);

            ItemsPanel.Children.Add(row);
        }
    }

    private void Toggle(LoginItem item, bool enable)
    {
        if (!RequireLicense()) return;
        Log.Clear();
        if (enable) LoginItemsService.Enable(item, line => Log.Append(line));
        else LoginItemsService.Disable(item, line => Log.Append(line));
        Rescan();
    }

    private bool RequireLicense()
    {
        if (LicenseManager.Shared.IsUnlocked) return true;
        var gate = new TrialGateWindow { Owner = Window.GetWindow(this) };
        return gate.ShowDialog() == true;
    }
}
