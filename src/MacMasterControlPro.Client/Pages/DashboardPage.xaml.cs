using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using MacMasterControlPro.Client.Services;
using MacMasterControlPro.Core.Services;

namespace MacMasterControlPro.Client.Pages;

public partial class DashboardPage : UserControl
{
    private static readonly FontFamily IconFont = new("Segoe Fluent Icons, Segoe MDL2 Assets");

    public DashboardPage()
    {
        InitializeComponent();
        TitleText.Text = "Master Control Studio Pro";
        TaglineText.Text = L.X("Spațiu, curățenie și controlul sistemului pentru stațiile de montaj.", "Space, cleanup and system control for editing workstations.", "Espacio, limpieza y control del sistema para estaciones de montaje.");
        DiskButton.Content = L.X("Vezi ce ocupă spațiul", "See what takes up space", "Ver qué ocupa espacio");
        CleanButton.Content = L.X("Curățare", "Cleanup", "Limpieza");
        CheckTitle.Text = L.X("Ce merită verificat", "Worth checking", "Qué conviene revisar");
        SizeChanged += (_, e) => RootGrid.Width = Math.Max(300, Math.Min(980, e.NewSize.Width - 24));
        ArtHost.Content = Services.StudioArt.Create("home-glass", 224, 168);
        RenderVolumes();
        RenderChecks(null, null);
        Task.Run(() =>
        {
            bool? sec = null, deps = null;
            try { sec = SecurityService.RunAllChecks().All(c => c.IsGood); } catch { }
            try { var d = new DependencyChecker(); d.CheckAll(); deps = d.Items.Count == 0 ? null : d.AllInstalled; } catch { }
            Dispatcher.Invoke(() => RenderChecks(sec, deps));
        });
    }

    private void RenderVolumes()
    {
        var allowed = DiskAnalyzerServiceRoots.AvailableRoots().Select(r => r.Path.TrimEnd('\\')).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var drive in DriveInfo.GetDrives().Where(d => d.IsReady && d.DriveType is DriveType.Fixed or DriveType.Removable or DriveType.Network).Where(d => allowed.Contains(d.RootDirectory.FullName.TrimEnd('\\'))))
        {
            long total = 0, free = 0;
            try { total = drive.TotalSize; free = drive.AvailableFreeSpace; } catch { }
            var used = Math.Max(0, total - free);
            var frac = total > 0 ? (double)used / total : 0;
            var icon = new Image { Width = 56, Height = 56, HorizontalAlignment = HorizontalAlignment.Center };
            ShellIconCache.GetAsync(drive.RootDirectory.FullName, 112).ContinueWith(t => { if (t.Result is { } s) icon.Dispatcher.Invoke(() => icon.Source = s); });
            var bar = new ProgressBar { Minimum = 0, Maximum = 1, Value = frac, Height = 6, Margin = new Thickness(0, 8, 0, 4) };
            var sp = new StackPanel { Width = 200 };
            sp.Children.Add(icon);
            sp.Children.Add(new TextBlock { Text = string.IsNullOrEmpty(drive.VolumeLabel) ? drive.Name : $"{drive.VolumeLabel} ({drive.Name.TrimEnd('\\')})", FontWeight = FontWeights.SemiBold, TextAlignment = TextAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis, Margin = new Thickness(0, 6, 0, 0) });
            sp.Children.Add(bar);
            sp.Children.Add(new TextBlock { Text = $"{DiskNode.FormatBytes(used)} "+L.X("folosit", "used", "usado")+$" · {DiskNode.FormatBytes(free)} "+L.X("liber", "free", "libre"), FontSize = 11.5, TextAlignment = TextAlignment.Center, Foreground = (Brush)FindResource("TextSecondaryBrush") });
            var card = new Border { Child = sp, Padding = new Thickness(16), Margin = new Thickness(0, 0, 14, 14), CornerRadius = new CornerRadius(16), Background = (Brush)FindResource("CardBackgroundBrush"), BorderBrush = (Brush)FindResource("CardStrokeBrush"), BorderThickness = new Thickness(1), Cursor = System.Windows.Input.Cursors.Hand, ToolTip = L.X("Analizează acest volum", "Analyze this volume", "Analizar este volumen") };
            System.Windows.Automation.AutomationProperties.SetName(card, $"{drive.Name}: {DiskNode.FormatBytes(used)} / {DiskNode.FormatBytes(total)}");
            card.MouseLeftButtonUp += (_, _) => Navigate("DiskAnalyzer");
            VolumeCards.Items.Add(card);
        }
    }

    private void RenderChecks(bool? security, bool? deps)
    {
        Checks.Children.Clear();
        AddCheck(L.X("Securitate", "Security", "Seguridad"), L.X("Firewall, Defender, actualizări", "Firewall, Defender, updates", "Firewall, Defender, actualizaciones"), security, "Security");
        AddCheck(L.X("Dependențe", "Dependencies", "Dependencias"), "rclone, WinFsp, winget", deps, "Dependencies");
    }

    private void AddCheck(string title, string detail, bool? good, string tag)
    {
        var glyph = good is null ? "" : good == true ? "" : "";
        var color = good is null ? (Brush)FindResource("TextSecondaryBrush") : good == true ? new SolidColorBrush(Color.FromRgb(0x11, 0x7A, 0x52)) : (Brush)FindResource("StatusErrorBrush");
        var g = new Grid { Margin = new Thickness(0, 8, 0, 8), Cursor = System.Windows.Input.Cursors.Hand };
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(34) }); g.ColumnDefinitions.Add(new ColumnDefinition()); g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        g.Children.Add(new TextBlock { Text = glyph, FontFamily = IconFont, FontSize = 20, Foreground = color, VerticalAlignment = VerticalAlignment.Center });
        var t = new StackPanel(); t.Children.Add(new TextBlock { Text = title, FontWeight = FontWeights.SemiBold });
        t.Children.Add(new TextBlock { Text = detail, FontSize = 11.5, Foreground = (Brush)FindResource("TextSecondaryBrush") });
        Grid.SetColumn(t, 1); g.Children.Add(t);
        var s = new TextBlock { Text = good is null ? L.X("Se verifică…", "Checking…", "Comprobando…") : good == true ? L.X("În regulă", "All good", "Correcto") : L.X("De verificat", "Needs review", "Por revisar"), Foreground = (Brush)FindResource("TextSecondaryBrush"), VerticalAlignment = VerticalAlignment.Center };
        Grid.SetColumn(s, 2); g.Children.Add(s);
        g.MouseLeftButtonUp += (_, _) => Navigate(tag);
        Checks.Children.Add(g);
        Checks.Children.Add(new Separator { Opacity = 0.15 });
    }

    private static void Navigate(string tag) => (Application.Current.MainWindow as MainWindow)?.NavigateTo(tag);
    private void OnDiskClicked(object sender, RoutedEventArgs e) => Navigate("DiskAnalyzer");
    private void OnCleanClicked(object sender, RoutedEventArgs e) => Navigate("Cleanup");
}
