using System.Windows;
using System.Windows.Controls;
using MacMasterControlPro.Core.Services;

namespace MacMasterControlPro.Client.Pages;

public partial class DiskHealthPage : UserControl
{
    private List<DiskHealth> _disks = new();

    public DiskHealthPage()
    {
        InitializeComponent();
        Rescan();
    }

    private void OnRescanClicked(object sender, RoutedEventArgs e) => Rescan();

    private void Rescan()
    {
        _disks = DiskHealthService.ScanVolumes();
        Render();
    }

    private void Render()
    {
        DisksPanel.Children.Clear();
        foreach (var disk in _disks)
        {
            var card = new Wpf.Ui.Controls.CardExpander { Header = disk.Name, IsExpanded = true, Margin = new Thickness(0, 0, 0, 10) };
            var stack = new StackPanel();

            var headerRow = new WrapPanel();
            if (disk.IsLowSpace)
                headerRow.Children.Add(new TextBlock { Text = "⚠ Spațiu redus", Foreground = System.Windows.Media.Brushes.Orange, FontSize = 11, Margin = new Thickness(0, 0, 12, 0) });
            if (disk.IsFailing)
                headerRow.Children.Add(new TextBlock { Text = "✘ SMART: posibilă defecțiune", Foreground = System.Windows.Media.Brushes.OrangeRed, FontSize = 11, Margin = new Thickness(0, 0, 12, 0) });
            var testButton = new Wpf.Ui.Controls.Button { Content = "Testează viteza" };
            var speedText = new TextBlock { FontSize = 11, Foreground = System.Windows.Media.Brushes.Gray, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(8, 0, 0, 0) };
            testButton.Click += (_, _) =>
            {
                testButton.IsEnabled = false;
                speedText.Text = "Se testează…";
                Task.Run(() =>
                {
                    var (speed, error) = DiskHealthService.MeasureWriteSpeed(disk.DriveLetter);
                    Dispatcher.Invoke(() =>
                    {
                        speedText.Text = speed.HasValue ? $"Scriere: {speed:F0} MB/s" : (error ?? "Eroare la test");
                        speedText.Foreground = speed.HasValue ? System.Windows.Media.Brushes.Gray : System.Windows.Media.Brushes.OrangeRed;
                        testButton.IsEnabled = true;
                    });
                });
            };
            headerRow.Children.Add(testButton);
            headerRow.Children.Add(speedText);
            stack.Children.Add(headerRow);

            var progress = new ProgressBar { Value = 100 - disk.FreePercent, Maximum = 100, Height = 6, Margin = new Thickness(0, 8, 0, 4) };
            stack.Children.Add(progress);

            var freeGb = disk.AvailableBytes / 1_073_741_824.0;
            var totalGb = disk.TotalBytes / 1_073_741_824.0;
            var infoLine = new WrapPanel();
            infoLine.Children.Add(new TextBlock { Text = $"{freeGb:F1} GB liberi din {totalGb:F1} GB ({disk.FreePercent:F0}% liber)", FontSize = 11, Foreground = System.Windows.Media.Brushes.Gray });
            if (disk.SmartStatus != null)
                infoLine.Children.Add(new TextBlock { Text = $"  ·  SMART: {disk.SmartStatus}", FontSize = 11, Foreground = System.Windows.Media.Brushes.Gray });
            stack.Children.Add(infoLine);

            card.Content = stack;
            DisksPanel.Children.Add(card);
        }
    }
}
