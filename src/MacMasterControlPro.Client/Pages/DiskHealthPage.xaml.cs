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
                headerRow.Children.Add(new TextBlock { Text = L.X("⚠ Spațiu redus", "⚠ Low space", "⚠ Poco espacio"), Foreground = System.Windows.Media.Brushes.Orange, FontSize = 11, Margin = new Thickness(0, 0, 12, 0) });
            if (disk.IsFailing)
                headerRow.Children.Add(new TextBlock { Text = L.X("✘ SMART: posibilă defecțiune", "✘ SMART: possible failure", "✘ SMART: posible fallo"), Foreground = System.Windows.Media.Brushes.OrangeRed, FontSize = 11, Margin = new Thickness(0, 0, 12, 0) });
            var testButton = new Wpf.Ui.Controls.Button
            {
                Content = L.X("Testează viteza", "Test speed", "Probar velocidad"),
                ToolTip = L.X("Scrie temporar 256 MB pe acest disc și măsoară cât durează — un test real de viteză.", "Temporarily writes 256 MB to this drive and measures how long it takes — a real speed test.", "Escribe temporalmente 256 MB en este disco y mide cuánto tarda: una prueba real de velocidad."),
            };
            var speedText = new TextBlock { FontSize = 11, Foreground = System.Windows.Media.Brushes.Gray, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(8, 0, 0, 0) };
            testButton.Click += (_, _) =>
            {
                testButton.IsEnabled = false;
                speedText.Text = L.X("Se testează…", "Testing…", "Probando…");
                Task.Run(() =>
                {
                    var (speed, error) = DiskHealthService.MeasureWriteSpeed(disk.DriveLetter);
                    Dispatcher.Invoke(() =>
                    {
                        speedText.Text = speed.HasValue ? $"Scriere: {speed:F0} MB/s" : (error ?? L.X("Eroare la test", "Test error", "Error en la prueba"));
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
            infoLine.Children.Add(new TextBlock { Text = L.X($"{freeGb:F1} GB liberi din {totalGb:F1} GB ({disk.FreePercent:F0}% liber)", $"{freeGb:F1} GB free of {totalGb:F1} GB ({disk.FreePercent:F0}% free)", $"{freeGb:F1} GB libres de {totalGb:F1} GB ({disk.FreePercent:F0}% libre)"), FontSize = 11, Foreground = System.Windows.Media.Brushes.Gray });
            if (disk.SmartStatus != null)
                infoLine.Children.Add(new TextBlock { Text = $"  ·  SMART: {disk.SmartStatus}", FontSize = 11, Foreground = System.Windows.Media.Brushes.Gray });
            stack.Children.Add(infoLine);

            card.Content = stack;
            DisksPanel.Children.Add(card);
        }
    }
}
