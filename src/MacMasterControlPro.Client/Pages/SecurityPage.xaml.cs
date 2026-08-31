using System.Windows;
using System.Windows.Controls;
using MacMasterControlPro.Core.Services;

namespace MacMasterControlPro.Client.Pages;

public partial class SecurityPage : UserControl
{
    public SecurityPage()
    {
        InitializeComponent();
        Rescan();
    }

    private void OnRescanClicked(object sender, RoutedEventArgs e) => Rescan();

    private void Rescan()
    {
        ChecksPanel.Children.Clear();
        foreach (var check in SecurityService.RunAllChecks())
        {
            var row = new Grid { Margin = new Thickness(0, 4, 0, 4) };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var dot = new System.Windows.Shapes.Ellipse
            {
                Width = 8,
                Height = 8,
                Fill = check.IsGood ? System.Windows.Media.Brushes.LimeGreen : System.Windows.Media.Brushes.OrangeRed,
                Margin = new Thickness(0, 0, 8, 0),
            };
            Grid.SetColumn(dot, 0);
            row.Children.Add(dot);

            var title = new TextBlock { Text = check.Title, VerticalAlignment = VerticalAlignment.Center };
            Grid.SetColumn(title, 1);
            row.Children.Add(title);

            var detail = new TextBlock
            {
                Text = check.Detail,
                Foreground = System.Windows.Media.Brushes.Gray,
                FontSize = 10,
                TextTrimming = TextTrimming.CharacterEllipsis,
                MaxWidth = 220,
            };
            Grid.SetColumn(detail, 2);
            row.Children.Add(detail);

            ChecksPanel.Children.Add(row);
        }
    }

    private void OnEnableFirewallClicked(object sender, RoutedEventArgs e)
    {
        if (!RequireLicense()) return;
        var ok = SecurityService.EnableFirewallAllProfiles();
        StatusText.Text = ok ? "✔ Firewall activat pe toate profilurile." : "EROARE — verifică promptul UAC.";
        Rescan();
    }

    private void OnRequirePasswordClicked(object sender, RoutedEventArgs e)
    {
        if (!RequireLicense()) return;
        var ok = SecurityService.RequirePasswordImmediatelyOnWake();
        StatusText.Text = ok ? "✔ Parola va fi cerută imediat la trezire." : "EROARE — verifică promptul UAC.";
    }

    private bool RequireLicense()
    {
        if (LicenseManager.Shared.IsUnlocked) return true;
        var gate = new TrialGateWindow { Owner = Window.GetWindow(this) };
        return gate.ShowDialog() == true;
    }
}
