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

            // BUG REAL/cerinta (oglinda fix-ului Mac, 2026-08-31: "doar
            // imi arata rosu/verde, nu ma ajuta cu nimic sa rezolv") -
            // buton "Cum rezolv?" pentru orice verificare rosie cu pasi
            // expliciti disponibili.
            if (!check.IsGood && check.ManualSteps.Count > 0)
            {
                var guideButton = new Wpf.Ui.Controls.Button { Content = "Cum rezolv?", Margin = new Thickness(16, 0, 0, 8), HorizontalAlignment = HorizontalAlignment.Left };
                guideButton.Click += (_, _) => ShowGuide(check);
                ChecksPanel.Children.Add(guideButton);
            }
        }
    }

    private void ShowGuide(SecurityCheck check)
    {
        var stack = new StackPanel { Margin = new Thickness(20) };
        stack.Children.Add(new TextBlock { Text = $"Cum activez: {check.Title}", FontSize = 16, FontWeight = FontWeights.Bold, Margin = new Thickness(0, 0, 0, 12) });
        for (var i = 0; i < check.ManualSteps.Count; i++)
        {
            var row = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 8) };
            row.Children.Add(new TextBlock { Text = $"{i + 1}.", FontWeight = FontWeights.Bold, Width = 20 });
            row.Children.Add(new TextBlock { Text = check.ManualSteps[i], TextWrapping = TextWrapping.Wrap, MaxWidth = 360 });
            stack.Children.Add(row);
        }
        if (check.SettingsUri != null)
        {
            var openButton = new Wpf.Ui.Controls.Button { Content = "Deschide Settings", Appearance = Wpf.Ui.Controls.ControlAppearance.Primary, Margin = new Thickness(0, 12, 0, 0) };
            openButton.Click += (_, _) =>
            {
                try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(check.SettingsUri!) { UseShellExecute = true }); }
                catch { /* utilizatorul poate naviga manual daca link-ul nu porneste */ }
            };
            stack.Children.Add(openButton);
        }
        var closeButton = new Wpf.Ui.Controls.Button { Content = "Închide", Margin = new Thickness(0, 8, 0, 0) };
        var window = new Window
        {
            Title = check.Title,
            Content = stack,
            SizeToContent = SizeToContent.WidthAndHeight,
            Owner = Window.GetWindow(this),
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            ResizeMode = ResizeMode.NoResize,
        };
        closeButton.Click += (_, _) => window.Close();
        stack.Children.Add(closeButton);
        window.ShowDialog();
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
