using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using MacMasterControlPro.Core.Services;

namespace MacMasterControlPro.Client.Pages;

public partial class ProcessMonitorPage : UserControl
{
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromSeconds(3) };
    // Cerinta (oglinda fix-ului Mac, 2026-08-31: "sa pot selecta cel mai
    // mare SAU cel mai mic consumator") - lista e deja sortata descrescator
    // de TopProcesses(); acest toggle doar o inverseaza pentru RAM.
    private bool _largestFirst = true;

    public ProcessMonitorPage()
    {
        InitializeComponent();
        Refresh();
        _timer.Tick += (_, _) => Refresh();
        _timer.Start();
        Unloaded += (_, _) => _timer.Stop();
    }

    private void OnRefreshClicked(object sender, RoutedEventArgs e) => Refresh();

    private void OnToggleSortClicked(object sender, RoutedEventArgs e)
    {
        _largestFirst = !_largestFirst;
        SortOrderButton.Content = _largestFirst ? "Cel mai mare întâi" : "Cel mai mic întâi";
        Refresh();
    }

    private void Refresh()
    {
        var processes = ProcessMonitorService.TopProcesses();
        if (!_largestFirst) processes.Reverse();
        ItemsPanel.Children.Clear();
        foreach (var process in processes)
        {
            var row = new Grid { Margin = new Thickness(0, 3, 0, 3) };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var name = new TextBlock { Text = process.Name, VerticalAlignment = VerticalAlignment.Center };
            Grid.SetColumn(name, 0);
            row.Children.Add(name);

            var mem = new TextBlock
            {
                Text = $"{process.MemoryMB:0} MB",
                Foreground = System.Windows.Media.Brushes.Gray,
                FontFamily = new System.Windows.Media.FontFamily("Consolas"),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 10, 0),
            };
            Grid.SetColumn(mem, 1);
            row.Children.Add(mem);

            var closeButton = new Wpf.Ui.Controls.Button
            {
                Content = "Închide", Padding = new Thickness(8, 2, 8, 2),
                ToolTip = "Cere procesului să se închidă normal.",
            };
            closeButton.Click += (_, _) => { ProcessMonitorService.Terminate(process.Pid, force: false); Refresh(); };
            Grid.SetColumn(closeButton, 2);
            row.Children.Add(closeButton);

            ItemsPanel.Children.Add(row);
        }
    }
}
