using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using MacMasterControlPro.Client.Controls;
using MacMasterControlPro.Client.Services;
using MacMasterControlPro.Core.Services;

namespace MacMasterControlPro.Client.Pages;

/// Analiză de disc, direcția „Studio Glass”: hartă radială mare, listă scurtă, detalii la cerere, selecție vizibilă și o acțiune principală.
/// Sectoarele sunt proporționale cu mărimea reală; selecția e comună între hartă și listă; ștergerea e DOAR mutare recuperabilă în Coș.
public partial class DiskAnalyzerPage : UserControl
{
    private static System.Globalization.CultureInfo Culture => new(L.X("ro-RO", "en-GB", "es-ES"));
    private DiskNode? _tree;
    private readonly List<DiskNode> _pathStack = new();
    private string? _currentRootPath;
    private DateTime? _lastScannedAtUtc, _verifiedAtUtc;
    private bool _isIndexing, _isRescanning, _showAll, _advanced, _confirmUnverified;
    private int _accessErrors;
    private string? _scanNotice;
    private CancellationTokenSource? _cts;
    private IDisposable? _busy;
    private readonly Dictionary<string, DiskNode> _selection = new(StringComparer.OrdinalIgnoreCase);
    private DiskNode? _focus;
    private string? _openDetail;
    private (long Reg, long Dup, long Overlap, long Rec)? _totals;
    private Dictionary<string, RecycleBinService.Outcome>? _results;
    private List<DiskNode> _resultItems = new();
    private bool _trashing;

    private static readonly ISet<string> NoDuplicates = new HashSet<string>();
    private DiskNode? CurrentNode => _pathStack.Count > 0 ? _pathStack[^1] : _tree;
    private List<DiskNode> CurrentChildren => CurrentNode?.SortedChildren.ToList() ?? new();

    private void ApplyLanguage()
    {
        HeadTitle.Text = L.X("Ce ocupă spațiul", "What takes up space", "Qué ocupa espacio");
        HeadSub.Text = L.X("Alege un volum, intră în foldere, bifează ce vrei să revizuiești.", "Choose a volume, step into folders, tick what you want to review.", "Elige un volumen, entra en las carpetas y marca lo que quieras revisar.");
        AdvancedToggle.Content = L.X("Tabel avansat", "Advanced table", "Tabla avanzada");
        PickerHint.Text = L.X("Alege un volum ca să-l analizezi. Rezultatul rămâne salvat; la următoarea deschidere nu se reia scanarea.", "Choose a volume to analyze it. The result is saved; the next time you open it, it is not rescanned.", "Elige un volumen para analizarlo. El resultado se guarda y no se vuelve a escanear al abrirlo.");
        StopScanButton.Content = L.X("Oprește", "Stop", "Detener"); StopRescanButton.Content = StopScanButton.Content;
        ScanHint.Text = L.X("O singură trecere completă. După ce se termină, orice subfolder se deschide instant.", "One full pass. Once it finishes, any subfolder opens instantly.", "Una sola pasada completa. Al terminar, cualquier subcarpeta se abre al instante.");
        RescanButton.Content = L.X("Verifică modificările", "Check for changes", "Comprobar cambios");
        ResetCacheButton.Content = L.X("Resetează cache", "Reset cache", "Restablecer caché");
        CalcButton.Content = L.X("Cum se calculează", "How it is calculated", "Cómo se calcula");
        ClearButton.Content = L.X("Deselectează", "Clear selection", "Deseleccionar");
        ReviewButton.Content = L.X("Revizuiește selecția", "Review selection", "Revisar selección");
        SheetBack.Content = L.X("Înapoi", "Back", "Atrás");
        AdvancedGrid.Columns[0].Header = L.X("Nume", "Name", "Nombre"); AdvancedGrid.Columns[1].Header = L.X("Stare", "State", "Estado");
        AdvancedGrid.Columns[2].Header = L.X("Logic", "Logical", "Lógico"); AdvancedGrid.Columns[3].Header = L.X("Alocat", "Allocated", "Asignado");
    }

    public DiskAnalyzerPage()
    {
        InitializeComponent();
        ApplyLanguage();
        Chart.Tapped += n => { _focus = n; if (!ToggleSelection(n)) System.Media.SystemSounds.Beep.Play(); RenderResult(); };
        Chart.Drilled += n => Drill(n);
        Chart.HoverChanged += n => HighlightRow(n);
        RenderVolumes();
        RenderState();
    }

    // MARK: - Volume (iconuri Shell, asincron)

    private void RenderVolumes()
    {
        VolumesList.Items.Clear();
        var pick = new Button { Background = Brushes.Transparent, BorderThickness = new Thickness(0), Padding = new Thickness(6) };
        var ps = new StackPanel { Width = 112, Margin = new Thickness(4) };
        ps.Children.Add(new TextBlock { Text = "\uE8F4", FontFamily = new FontFamily("Segoe Fluent Icons, Segoe MDL2 Assets"), FontSize = 34, HorizontalAlignment = HorizontalAlignment.Center, Foreground = (Brush)FindResource("LinkAccentBrush"), Margin = new Thickness(0, 6, 0, 6) });
        ps.Children.Add(new TextBlock { Text = L.X("Alege un folder", "Choose a folder", "Elige una carpeta"), TextAlignment = TextAlignment.Center, FontWeight = FontWeights.Medium });
        ps.Children.Add(new TextBlock { Text = L.X("orice folder sau volum", "any folder or volume", "cualquier carpeta o volumen"), FontSize = 11, TextAlignment = TextAlignment.Center, Foreground = (Brush)FindResource("TextSecondaryBrush") });
        pick.Content = ps; pick.Click += (_, _) => ChooseFolder();
        VolumesList.Items.Add(pick);
        foreach (var root in DiskAnalyzerServiceRoots.AvailableRoots())
        {
            var icon = MakeIcon(root.Path, 48);
            var stack = new StackPanel { Width = 112, Margin = new Thickness(4) };
            icon.HorizontalAlignment = HorizontalAlignment.Center;
            stack.Children.Add(icon);
            stack.Children.Add(new TextBlock { Text = root.Name, TextAlignment = TextAlignment.Center, FontWeight = FontWeights.Medium, TextTrimming = TextTrimming.CharacterEllipsis, Margin = new Thickness(0, 4, 0, 0) });
            stack.Children.Add(new TextBlock { Text = root.SizeDescription, FontSize = 11, TextAlignment = TextAlignment.Center, Foreground = (Brush)FindResource("TextSecondaryBrush") });
            var b = new Button { Content = stack, Background = Brushes.Transparent, BorderThickness = new Thickness(0), Padding = new Thickness(6), ToolTip = root.Path };
            b.Click += (_, _) => StartIndexing(root);
            System.Windows.Automation.AutomationProperties.SetName(b, $"{root.Name}");
            VolumesList.Items.Add(b);
        }
    }

    private void ChooseFolder()
    {
        var dlg = new Microsoft.Win32.OpenFolderDialog { Title = L.X("Alege folderul sau volumul de analizat", "Choose the folder or volume to analyze", "Elige la carpeta o el volumen que analizar") };
        if (dlg.ShowDialog() != true) return;
        StartIndexing(new DiskEntry(System.IO.Path.GetFileName(dlg.FolderName.TrimEnd('\\')) is { Length: > 0 } n ? n : dlg.FolderName, dlg.FolderName, 0));
    }

    private static Image MakeIcon(string path, int size)
    {
        var img = new Image { Width = size, Height = size };
        if (ShellIconCache.Cached(path, size * 2) is { } hit) img.Source = hit;
        else ShellIconCache.GetAsync(path, size * 2).ContinueWith(t => { if (t.Result is { } s) img.Dispatcher.Invoke(() => img.Source = s); });
        return img;
    }

    // MARK: - Scanare / cache (logica verificată în Lot 2, neschimbată)

    private void StartIndexing(DiskEntry root)
    {
        _currentRootPath = root.Path; _pathStack.Clear(); _focus = null; _scanNotice = null;
        var cached = DiskCacheStore.Load(root.Path);
        if (cached is not null)
        {
            _tree = cached.Root; _lastScannedAtUtc = cached.ScannedAtUtc; _verifiedAtUtc = cached.VerifiedAtUtc; _accessErrors = cached.AccessErrors;
            RenderResult(); return;
        }
        PerformFullScan(root.Path);
    }

    private void PerformFullScan(string rootPath)
    {
        _tree = null; _isIndexing = true; RenderState();
        IndexingStatusText.Text = string.Format(L.X("Indexez… {0} fișiere până acum.", "Indexing… {0} files so far.", "Indexando… {0} archivos hasta ahora."), 0);
        _cts = new CancellationTokenSource(); var ct = _cts.Token;
        _busy = AccessCoordinator.Busy();
        Task.Run(() =>
        {
            var (node, outcome) = DiskScanEngine.BuildTree(rootPath, progress => Dispatcher.Invoke(() =>
                IndexingStatusText.Text = string.Format(L.X("Indexez… {0:N0} fișiere, {1} până acum.", "Indexing… {0:N0} files, {1} so far.", "Indexando… {0:N0} archivos, {1} hasta ahora."), progress.FilesIndexed, DiskNode.FormatBytes(progress.BytesIndexed))), ct);
            Dispatcher.Invoke(() =>
            {
                _busy?.Dispose(); _busy = null; _isIndexing = false;
                if (outcome.Status != DiskScanEngine.ScanStatus.Complete)
                {
                    _scanNotice = outcome.Status == DiskScanEngine.ScanStatus.RootUnavailable ? L.X("Unitatea nu mai este disponibilă. Nu am salvat nimic.", "The drive is no longer available. Nothing was saved.", "La unidad ya no está disponible. No se guardó nada.") : null;
                    _tree = null; RenderState(); return;
                }
                _tree = node; _lastScannedAtUtc = DateTime.UtcNow; _verifiedAtUtc = _lastScannedAtUtc; _accessErrors = outcome.AccessErrors;
                DiskCacheStore.Save(new DiskCacheStore.Snapshot { RootPath = rootPath, ScannedAtUtc = _lastScannedAtUtc.Value, Root = node, AccessErrors = outcome.AccessErrors, VerifiedAtUtc = _verifiedAtUtc });
                RenderResult();
            });
        });
    }

    private void OnStopScanClicked(object sender, RoutedEventArgs e) => _cts?.Cancel();

    private void OnRescanClicked(object sender, RoutedEventArgs e)
    {
        if (_tree is null || _currentRootPath is null || _isRescanning || _isIndexing) return;
        _isRescanning = true; _scanNotice = null; RenderResult();
        var rootPath = _currentRootPath; var tree = _tree;
        _cts = new CancellationTokenSource(); var ct = _cts.Token;
        _busy = AccessCoordinator.Busy();
        Task.Run(() =>
        {
            var (node, outcome) = DiskScanEngine.IncrementalUpdate(tree, _ => { }, DiskScanEngine.IncrementalMode.Verified, ct);
            Dispatcher.Invoke(() =>
            {
                _busy?.Dispose(); _busy = null; _isRescanning = false;
                switch (outcome.Status)
                {
                    case DiskScanEngine.ScanStatus.Complete:
                        _tree = node; _lastScannedAtUtc = DateTime.UtcNow; _verifiedAtUtc = _lastScannedAtUtc; _accessErrors = outcome.AccessErrors;
                        DiskCacheStore.Save(new DiskCacheStore.Snapshot { RootPath = rootPath, ScannedAtUtc = _lastScannedAtUtc.Value, Root = node, AccessErrors = outcome.AccessErrors, VerifiedAtUtc = _verifiedAtUtc });
                        break;
                    case DiskScanEngine.ScanStatus.RootUnavailable:
                        _scanNotice = L.X("Unitatea nu mai este disponibilă. Datele afișate sunt din ultima analiză salvată.", "The drive is no longer available. The data shown is from the last saved analysis.", "La unidad ya no está disponible. Los datos mostrados son del último análisis guardado."); break;
                    default:
                        _scanNotice = L.X("Verificare anulată. S-a păstrat ultima analiză salvată.", "Check cancelled. The last saved analysis was kept.", "Comprobación cancelada. Se conservó el último análisis guardado.");
                        var snap = DiskCacheStore.Load(rootPath);
                        if (snap is not null) { _tree = snap.Root; _lastScannedAtUtc = snap.ScannedAtUtc; _verifiedAtUtc = snap.VerifiedAtUtc; _accessErrors = snap.AccessErrors; _pathStack.Clear(); }
                        break;
                }
                RenderResult();
            });
        });
    }

    private void OnResetCacheClicked(object sender, RoutedEventArgs e)
    {
        if (_currentRootPath is null) return;
        if (MessageBox.Show(L.X("Poate dura mult pe un disc mare. „Verifică modificările” e suficient în mod normal.\n\nResetezi cache-ul și rescanezi tot volumul?", "This can take long on a large drive. “Check for changes” is normally enough.\n\nReset the cache and rescan the whole volume?", "Puede tardar mucho en un disco grande. «Comprobar cambios» suele bastar.\n\n¿Restablecer la caché y volver a analizar todo el volumen?"), L.X("Confirmare", "Confirm", "Confirmar"), MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;
        DiskCacheStore.Clear(_currentRootPath); _pathStack.Clear(); _lastScannedAtUtc = null; PerformFullScan(_currentRootPath);
    }

    // MARK: - Selecție (fără dublă numărare)

    private bool ToggleSelection(DiskNode n)
    {
        if (_selection.Remove(n.Path)) return true;
        if (SpaceClassifier.Flags(n.Path, n.IsDirectory).Contains(SpaceFlag.Protected)) return false;
        foreach (var k in _selection.Keys.Where(k => k.StartsWith(n.Path.TrimEnd('\\') + "\\", StringComparison.OrdinalIgnoreCase)).ToList()) _selection.Remove(k);   // copiii înlocuiți de părinte
        var cur = System.IO.Path.GetDirectoryName(n.Path);
        while (!string.IsNullOrEmpty(cur)) { _selection.Remove(cur); cur = System.IO.Path.GetDirectoryName(cur); }
        _selection[n.Path] = n; return true;
    }

    // MARK: - Randare

    private void RenderState()
    {
        var hasTree = !_isIndexing && _tree is not null;
        PickerHint.Visibility = (!_isIndexing && _tree is null) ? Visibility.Visible : Visibility.Collapsed;
        IndexingPanel.Visibility = _isIndexing ? Visibility.Visible : Visibility.Collapsed;
        ResultPanel.Visibility = hasTree ? Visibility.Visible : Visibility.Collapsed;
        CrumbBar.Visibility = hasTree ? Visibility.Visible : Visibility.Collapsed;
        ViewToggle.Visibility = hasTree ? Visibility.Visible : Visibility.Collapsed;
        NoticeText.Visibility = hasTree && _scanNotice is not null ? Visibility.Visible : Visibility.Collapsed;
        NoticeText.Text = _scanNotice ?? "";
    }

    private void RenderResult()
    {
        RenderState();
        if (_tree is null) return;
        RenderBreadcrumb(); RenderFreshness();
        MapView.Visibility = _advanced ? Visibility.Collapsed : Visibility.Visible;
        AdvancedGrid.Visibility = _advanced ? Visibility.Visible : Visibility.Collapsed;
        Chart.SetData(CurrentNode, _selection.Keys);
        RenderPanel(); RenderTray(); RenderAdvanced(); RefreshTotals();
        if (Sheet.Visibility == Visibility.Visible) RenderSheet();
    }

    private void RenderFreshness()
    {
        FreshnessText.Text = (_lastScannedAtUtc is null ? "" : _verifiedAtUtc is { } v ? string.Format(Culture, L.X("Verificat {0:d MMMM, HH:mm}", "Verified {0:d MMMM, HH:mm}", "Verificado {0:d MMMM, HH:mm}"), v.ToLocalTime()) : string.Format(Culture, L.X("Din analiza de {0:d MMMM, HH:mm}, neverificată", "From the analysis of {0:d MMMM, HH:mm}, not verified", "Del análisis del {0:d MMMM, HH:mm}, sin verificar"), _lastScannedAtUtc.Value.ToLocalTime()))
            + (_accessErrors > 0 ? $" · {_accessErrors} "+L.X("inaccesibile", "inaccessible", "inaccesibles") : "");
        RescanSpinner.Visibility = StopRescanButton.Visibility = _isRescanning ? Visibility.Visible : Visibility.Collapsed;
        RescanButton.Visibility = ResetCacheButton.Visibility = _isRescanning ? Visibility.Collapsed : Visibility.Visible;
    }

    private void RenderBreadcrumb()
    {
        BreadcrumbPanel.Children.Clear();
        var rootBtn = new Wpf.Ui.Controls.Button { Content = _tree!.Name, Appearance = Wpf.Ui.Controls.ControlAppearance.Transparent, FontWeight = _pathStack.Count == 0 ? FontWeights.SemiBold : FontWeights.Normal };
        rootBtn.Click += (_, _) => { _pathStack.Clear(); _focus = null; RenderResult(); };
        BreadcrumbPanel.Children.Add(rootBtn);
        for (int i = 0; i < _pathStack.Count; i++)
        {
            var index = i;
            BreadcrumbPanel.Children.Add(new TextBlock { Text = "›", Foreground = (Brush)FindResource("TextSecondaryBrush"), VerticalAlignment = VerticalAlignment.Center });
            var crumb = new Wpf.Ui.Controls.Button { Content = _pathStack[i].Name, Appearance = Wpf.Ui.Controls.ControlAppearance.Transparent, FontWeight = i == _pathStack.Count - 1 ? FontWeights.SemiBold : FontWeights.Normal };
            crumb.Click += (_, _) => { _pathStack.RemoveRange(index + 1, _pathStack.Count - index - 1); _focus = null; RenderResult(); };
            BreadcrumbPanel.Children.Add(crumb);
        }
    }

    private void RenderPanel()
    {
        var f = _focus ?? CurrentNode!;
        FocusPanel.Children.Clear();
        FocusPanel.Children.Add(MakeIcon(f.Path, 56));
        var t = new StackPanel { Margin = new Thickness(12, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };
        t.Children.Add(new TextBlock { Text = f.Name, FontSize = 20, FontWeight = FontWeights.Bold, TextTrimming = TextTrimming.CharacterEllipsis, MaxWidth = 260 });
        t.Children.Add(new TextBlock { Text = DiskNode.FormatBytes(f.SizeBytes), FontSize = 15, Foreground = (Brush)FindResource("TextSecondaryBrush") });
        FocusPanel.Children.Add(t);
        if (f.IsDirectory && !ReferenceEquals(f, CurrentNode))
        {
            var open = new Wpf.Ui.Controls.Button { Content = L.X("Deschide  ›", "Open  ›", "Abrir  ›"), Margin = new Thickness(12, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };
            open.Click += (_, _) => Drill(f); FocusPanel.Children.Add(open);
        }
        EntryList.Children.Clear();
        var kids = CurrentChildren; var shown = _showAll ? kids : kids.Take(5).ToList();
        foreach (var k in shown) EntryList.Children.Add(BuildRow(k));
        if (kids.Count == 0) EntryList.Children.Add(new TextBlock { Text = L.X("Folder gol.", "Empty folder.", "Carpeta vacía."), Foreground = (Brush)FindResource("TextSecondaryBrush") });
        if (kids.Count > 5)
        {
            var more = new Wpf.Ui.Controls.Button { Content = _showAll ? L.X("Arată mai puține", "Show fewer", "Mostrar menos") : string.Format(L.X("Alte {0} elemente", "{0} more items", "{0} elementos más"), kids.Count - 5), Appearance = Wpf.Ui.Controls.ControlAppearance.Transparent, Margin = new Thickness(0, 6, 0, 0) };
            more.Click += (_, _) => { _showAll = !_showAll; RenderPanel(); }; EntryList.Children.Add(more);
        }
    }

    private static string FlagText(SpaceFlag f) => f switch { SpaceFlag.Regenerable => L.X("Regenerabil", "Regenerable", "Regenerable"), SpaceFlag.Duplicate => L.X("Duplicat verificat", "Verified duplicate", "Duplicado verificado"), _ => L.X("Protejat", "Protected", "Protegido") };
    private static string FlagGlyph(SpaceFlag f) => f switch { SpaceFlag.Regenerable => "", SpaceFlag.Duplicate => "", _ => "" };

    private UIElement BuildChip(SpaceFlag f)
    {
        var (fg, bg) = f switch
        {
            SpaceFlag.Regenerable => (Color.FromRgb(0x11, 0x7A, 0x52), Color.FromArgb(0x30, 0x2A, 0x9D, 0x6F)),
            SpaceFlag.Duplicate => (Color.FromRgb(0x5B, 0x3F, 0xC4), Color.FromArgb(0x30, 0x7A, 0x52, 0xD8)),
            _ => (Color.FromRgb(0x6B, 0x77, 0x85), Color.FromArgb(0x30, 0x80, 0x80, 0x80)),
        };
        var sp = new StackPanel { Orientation = Orientation.Horizontal };
        sp.Children.Add(new TextBlock { Text = FlagGlyph(f), FontFamily = new FontFamily("Segoe Fluent Icons, Segoe MDL2 Assets"), FontSize = 11, Foreground = new SolidColorBrush(fg), Margin = new Thickness(0, 0, 4, 0), VerticalAlignment = VerticalAlignment.Center });
        sp.Children.Add(new TextBlock { Text = FlagText(f), FontSize = 11, FontWeight = FontWeights.SemiBold, Foreground = new SolidColorBrush(fg) });
        return new Border { Child = sp, Background = new SolidColorBrush(bg), CornerRadius = new CornerRadius(f == SpaceFlag.Regenerable ? 10 : 4), Padding = new Thickness(7, 1, 7, 1), Margin = new Thickness(6, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };
    }

    private readonly Dictionary<string, Border> _rows = new(StringComparer.OrdinalIgnoreCase);

    private UIElement BuildRow(DiskNode n)
    {
        var flags = SpaceClassifier.Flags(n.Path, n.IsDirectory, false, NoDuplicates);
        var selected = _selection.ContainsKey(n.Path);
        var protectedItem = flags.Contains(SpaceFlag.Protected);
        var grid = new Grid { Margin = new Thickness(6, 7, 6, 7) };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }); grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var ck = new Button { Content = selected ? "" : "", FontFamily = new FontFamily("Segoe Fluent Icons, Segoe MDL2 Assets"), FontSize = 18, Background = Brushes.Transparent, BorderThickness = new Thickness(0), Padding = new Thickness(0), Width = 28,
            Foreground = selected ? (Brush)FindResource("LinkAccentBrush") : (Brush)FindResource("TextSecondaryBrush"), IsEnabled = !protectedItem || selected, ToolTip = protectedItem ? L.X("Protejat: nu poate fi selectat", "Protected: cannot be selected", "Protegido: no se puede seleccionar") : L.X("Selectează", "Select", "Seleccionar") };
        System.Windows.Automation.AutomationProperties.SetName(ck, (selected ? L.X("Deselectează ", "Deselect ", "Deseleccionar ") : L.X("Selectează ", "Select ", "Seleccionar ")) + n.Name);
        ck.Click += (_, _) => { if (!ToggleSelection(n)) System.Media.SystemSounds.Beep.Play(); RenderResult(); };
        grid.Children.Add(ck);
        var icon = MakeIcon(n.Path, 32); icon.Margin = new Thickness(6, 0, 10, 0); Grid.SetColumn(icon, 1); grid.Children.Add(icon);
        var name = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        name.Children.Add(new TextBlock { Text = n.Name, FontWeight = FontWeights.SemiBold, TextTrimming = TextTrimming.CharacterEllipsis });
        var cur = CurrentNode!.SizeBytes <= 0 ? 1 : CurrentNode.SizeBytes;
        name.Children.Add(new TextBlock { Text = $"{DiskNode.FormatBytes(n.SizeBytes)} · {(double)n.SizeBytes / cur:P0}", FontSize = 11.5, Foreground = (Brush)FindResource("TextSecondaryBrush") });
        Grid.SetColumn(name, 2); grid.Children.Add(name);
        var chips = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        foreach (var f in flags.OrderBy(x => x)) chips.Children.Add(BuildChip(f));
        Grid.SetColumn(chips, 3); grid.Children.Add(chips);
        var info = new Button { Content = "", FontFamily = new FontFamily("Segoe Fluent Icons, Segoe MDL2 Assets"), Background = Brushes.Transparent, BorderThickness = new Thickness(0), Width = 28, ToolTip = L.X("Detalii", "Details", "Detalles"), Foreground = (Brush)FindResource("TextSecondaryBrush") };
        info.Click += (_, _) => { _openDetail = _openDetail == n.Path ? null : n.Path; _focus = n; RenderResult(); };
        Grid.SetColumn(info, 4); grid.Children.Add(info);
        if (n.IsDirectory)
        {
            var go = new Button { Content = "", FontFamily = new FontFamily("Segoe Fluent Icons, Segoe MDL2 Assets"), Background = Brushes.Transparent, BorderThickness = new Thickness(0), Width = 28, ToolTip = $"{L.X("Deschide", "Open", "Abrir")} {n.Name}", Foreground = (Brush)FindResource("TextSecondaryBrush") };
            go.Click += (_, _) => Drill(n); Grid.SetColumn(go, 5); grid.Children.Add(go);
        }
        var border = new Border { Child = grid, CornerRadius = new CornerRadius(10), Background = selected ? new SolidColorBrush(Color.FromArgb(0x26, 0x2F, 0x6B, 0xF0)) : Brushes.Transparent };
        border.MouseEnter += (_, _) => { Chart.SetHover(n); };
        border.MouseLeave += (_, _) => { Chart.SetHover(null); };
        border.MouseLeftButtonUp += (_, e) => { if (e.OriginalSource is DependencyObject d && IsButton(d)) return; _focus = n; RenderResult(); };
        _rows[n.Path] = border;
        var host = new StackPanel(); host.Children.Add(border);
        if (_openDetail == n.Path) host.Children.Add(BuildDetail(n, flags));
        host.Children.Add(new Separator { Opacity = 0.15, Margin = new Thickness(0, 1, 0, 1) });
        return host;
    }

    private static bool IsButton(DependencyObject d) { while (d is not null) { if (d is Button) return true; d = VisualTreeHelper.GetParent(d); } return false; }

    private void HighlightRow(DiskNode? n)
    {
        foreach (var kv in _rows) if (!_selection.ContainsKey(kv.Key)) kv.Value.Background = n is not null && kv.Key.Equals(n.Path, StringComparison.OrdinalIgnoreCase) ? new SolidColorBrush(Color.FromArgb(0x16, 0x2F, 0x6B, 0xF0)) : Brushes.Transparent;
    }

    private UIElement BuildDetail(DiskNode n, ISet<SpaceFlag> flags)
    {
        var g = new Grid { Margin = new Thickness(40, 2, 0, 6) };
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(120) }); g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        void Add(string k, string v) { var r = g.RowDefinitions.Count; g.RowDefinitions.Add(new RowDefinition()); var a = new TextBlock { Text = k, FontSize = 12, Foreground = (Brush)FindResource("TextSecondaryBrush") }; var b = new TextBlock { Text = v, FontSize = 12, TextWrapping = TextWrapping.Wrap }; Grid.SetRow(a, r); Grid.SetRow(b, r); Grid.SetColumn(b, 1); g.Children.Add(a); g.Children.Add(b); }
        Add(L.X("Cale", "Path", "Ruta"), n.Path); Add(L.X("Mărime logică", "Logical size", "Tamaño lógico"), DiskNode.FormatBytes(n.SizeBytes));
        if (n.AllocatedBytes > 0) Add(L.X("Spațiu alocat", "Allocated space", "Espacio asignado"), DiskNode.FormatBytes(n.AllocatedBytes));
        Add(L.X("De ce această stare", "Why this state", "Por qué este estado"), SpaceClassifier.Explanation(n.Path, n.IsDirectory, flags, L.X("ro", "en", "es")));
        var open = new Wpf.Ui.Controls.Button { Content = L.X("Arată în Explorer", "Show in Explorer", "Mostrar en el Explorador"), FontSize = 12, Margin = new Thickness(0, 6, 0, 0) };
        open.Click += (_, _) => OpenInExplorer(n);
        var r2 = g.RowDefinitions.Count; g.RowDefinitions.Add(new RowDefinition()); Grid.SetRow(open, r2); Grid.SetColumn(open, 1); g.Children.Add(open);
        return new Border { Child = g, BorderBrush = (Brush)FindResource("CardStrokeBrush"), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(8), Background = (Brush)FindResource("CardBackgroundBrush"), Margin = new Thickness(0, 2, 0, 4) };
    }

    private static void OpenInExplorer(DiskNode n)
    {
        try { if (n.IsDirectory) Process.Start("explorer.exe", $"\"{n.Path}\""); else Process.Start("explorer.exe", $"/select,\"{n.Path}\""); } catch { }
    }

    private void Drill(DiskNode n) { if (!n.IsDirectory) return; _pathStack.Add(n); _focus = null; _showAll = false; _openDetail = null; RenderResult(); }
    private void OnViewToggled(object sender, RoutedEventArgs e) { _advanced = AdvancedToggle.IsChecked == true; if (_tree is not null) RenderResult(); }

    private void RenderAdvanced()
    {
        if (!_advanced) return;
        var cur = CurrentNode!.SizeBytes <= 0 ? 1 : CurrentNode.SizeBytes;
        var rows = new List<object>();
        foreach (var k in CurrentChildren)
        {
            void Add(DiskNode x, string indent) => rows.Add(new { Name = indent + x.Name, State = string.Join(", ", SpaceClassifier.Flags(x.Path, x.IsDirectory).Select(FlagText)), Logical = DiskNode.FormatBytes(x.SizeBytes), Allocated = x.AllocatedBytes > 0 ? DiskNode.FormatBytes(x.AllocatedBytes) : "—", Percent = $"{(double)x.SizeBytes / cur:P1}" });
            Add(k, ""); foreach (var c in k.SortedChildren.Take(50)) Add(c, "    ");
        }
        AdvancedGrid.ItemsSource = rows;
    }

    // MARK: - Total recuperabil, tavă, revizuire

    private void RefreshTotals()
    {
        var tree = _tree; if (tree is null) return;
        RecoverableText.Text = _totals is { } t ? string.Format(L.X("Recuperabil verificat în volum: {0}.", "Verified recoverable in this volume: {0}.", "Recuperable verificado en el volumen: {0}."), DiskNode.FormatBytes(t.Rec)) : L.X("Calculez ce se poate recupera…", "Working out what can be recovered…", "Calculando lo que se puede recuperar…");
        if (_totals is not null) return;
        Task.Run(() => SpaceMath.VolumeTotals(tree, NoDuplicates)).ContinueWith(r => Dispatcher.Invoke(() =>
        { _totals = r.Result; RecoverableText.Text = string.Format(L.X("Recuperabil verificat în volum: {0}.", "Verified recoverable in this volume: {0}.", "Recuperable verificado en el volumen: {0}."), DiskNode.FormatBytes(_totals.Value.Rec)); }));
    }

    private void OnCalcClicked(object sender, RoutedEventArgs e)
    {
        if (_totals is not { } t) return;
        MessageBox.Show(string.Format(L.X("Calcul fără dublă numărare\n\nRegenerabil: {0}\nSurplus de duplicate verificate: {1}\nDuplicate din interiorul unui element regenerabil, numărate o singură dată: {2}\n\nTotal: {3}\n\nElementele fără regulă verificată nu intră în total.", "Calculation without double counting\n\nRegenerable: {0}\nVerified duplicate surplus: {1}\nDuplicates inside a regenerable item, counted once: {2}\n\nTotal: {3}\n\nItems with no verified rule are not included in the total.", "Cálculo sin doble recuento\n\nRegenerable: {0}\nExcedente de duplicados verificados: {1}\nDuplicados dentro de un elemento regenerable, contados una vez: {2}\n\nTotal: {3}\n\nLos elementos sin regla verificada no entran en el total."), DiskNode.FormatBytes(t.Reg), DiskNode.FormatBytes(t.Dup), DiskNode.FormatBytes(t.Overlap), DiskNode.FormatBytes(t.Rec)), L.X("Cum se calculează", "How it is calculated", "Cómo se calcula"));
    }

    private void RenderTray()
    {
        var s = SpaceMath.Summarize(_selection.Values, NoDuplicates);
        var n = s.Recoverable.Count + s.Unverified.Count;
        Tray.Visibility = n == 0 ? Visibility.Collapsed : Visibility.Visible;
        TrayTitle.Text = $"{n} {(n == 1 ? L.X("element selectat", "item selected", "elemento seleccionado") : L.X("elemente selectate", "items selected", "elementos seleccionados"))} · {DiskNode.FormatBytes(s.MoveBytes)}";
        TraySub.Text = $"{DiskNode.FormatBytes(s.RecoverableBytes)} "+L.X("recuperabili verificat", "verified recoverable", "recuperable verificado") + (s.UnverifiedBytes > 0 ? $" · {DiskNode.FormatBytes(s.UnverifiedBytes)} "+L.X("fără regulă verificată, nu intră în total", "no verified rule, not in the total", "sin regla verificada, no entra en el total") : "");
    }

    private void OnClearSelection(object sender, RoutedEventArgs e) { _selection.Clear(); RenderResult(); }
    private void OnReviewClicked(object sender, RoutedEventArgs e) { _results = null; Sheet.Visibility = Visibility.Visible; RenderSheet(); }
    private void OnSheetBack(object sender, RoutedEventArgs e) { Sheet.Visibility = Visibility.Collapsed; _results = null; RenderResult(); }

    private void RenderSheet()
    {
        var s = SpaceMath.Summarize(_selection.Values, NoDuplicates);
        var res = _results;
        SheetTitle.Text = res is null ? L.X("Revizuiește înainte de a muta în Coș", "Review before moving to the Recycle Bin", "Revisa antes de mover a la Papelera") : string.Format(L.X("Rezultat: {0} din {1} mutate în Coșul de reciclare", "Result: {0} of {1} moved to the Recycle Bin", "Resultado: {0} de {1} movidos a la Papelera de reciclaje"), res.Values.Count(o => o.Moved), res.Count);
        SheetSub.Text = res is null ? L.X("Nimic nu se șterge definitiv. Poți restaura din Coșul de reciclare.", "Nothing is permanently deleted. You can restore from the Recycle Bin.", "No se borra nada de forma definitiva. Puedes restaurar desde la Papelera de reciclaje.") : L.X("Elementele eșuate nu au fost modificate. Cele mutate se pot restaura din Coșul de reciclare.", "Failed items were not changed. Moved items can be restored from the Recycle Bin.", "Los elementos fallidos no se modificaron. Los movidos se pueden restaurar desde la Papelera de reciclaje.");
        SheetBody.Children.Clear();
        var items = res is null ? _selection.Values.ToList() : _resultItems;
        var recPaths = new HashSet<string>(s.Recoverable, StringComparer.OrdinalIgnoreCase);
        bool IsRec(DiskNode n) => res is null ? recPaths.Contains(n.Path) : SpaceClassifier.Flags(n.Path, n.IsDirectory).Any(f => f != SpaceFlag.Protected);
        SheetBody.Children.Add(Group(L.X("Recuperabile verificat", "Verified recoverable", "Recuperable verificado"), items.Where(IsRec).ToList(), res, false));
        var risky = items.Where(n => !IsRec(n) && !SpaceClassifier.Flags(n.Path, n.IsDirectory).Contains(SpaceFlag.Protected)).ToList();
        if (risky.Count > 0)
        {
            SheetBody.Children.Add(Group(L.X("Fără regulă verificată", "No verified rule", "Sin regla verificada"), risky, res, true));
            if (res is null)
            {
                var cb = new CheckBox { IsChecked = _confirmUnverified, Margin = new Thickness(6, 8, 0, 0), Content = new TextBlock { Text = L.X("Include și elementele fără regulă verificată. Nu știm dacă au o copie de rezervă; nu intră în totalul recuperabil.", "Also include items with no verified rule. We do not know whether they have a backup; they do not count toward the recoverable total.", "Incluir también los elementos sin regla verificada. No sabemos si tienen copia de seguridad; no cuentan en el total recuperable."), TextWrapping = TextWrapping.Wrap } };
                cb.Checked += (_, _) => { _confirmUnverified = true; RenderSheet(); }; cb.Unchecked += (_, _) => { _confirmUnverified = false; RenderSheet(); };
                SheetBody.Children.Add(cb);
            }
        }
        SheetBody.Children.Add(new TextBlock { Text = L.X("Excluse automat: backup-uri de dispozitiv, foldere de sistem, aplicații instalate și foldere personale întregi.", "Excluded automatically: device backups, system folders, installed apps and whole personal folders.", "Excluidos automáticamente: copias de dispositivos, carpetas del sistema, apps instaladas y carpetas personales enteras."), FontSize = 11.5, Foreground = (Brush)FindResource("TextSecondaryBrush"), Margin = new Thickness(6, 12, 0, 0), TextWrapping = TextWrapping.Wrap });
        var move = s.RecoverableBytes + (_confirmUnverified ? s.UnverifiedBytes : 0);
        SheetTotal.Text = string.Format(L.X("Total de mutat: {0}", "Total to move: {0}", "Total a mover: {0}"), DiskNode.FormatBytes(move));
        SheetBack.Content = res is null ? L.X("Înapoi", "Back", "Atrás") : L.X("Închide", "Close", "Cerrar");
        SheetRun.Visibility = res is null ? Visibility.Visible : Visibility.Collapsed;
        SheetRun.Content = string.Format(L.X("Mută {0} în Coș", "Move {0} to the Recycle Bin", "Mover {0} a la Papelera"), DiskNode.FormatBytes(move));
        SheetRun.IsEnabled = !_trashing && (s.Recoverable.Count > 0 || (_confirmUnverified && s.Unverified.Count > 0));
    }

    private UIElement Group(string title, List<DiskNode> nodes, Dictionary<string, RecycleBinService.Outcome>? res, bool risk)
    {
        var panel = new StackPanel();
        var head = new Grid { Margin = new Thickness(12, 10, 12, 6) };
        head.ColumnDefinitions.Add(new ColumnDefinition()); head.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        head.Children.Add(new TextBlock { Text = title, FontWeight = FontWeights.SemiBold });
        var sum = new TextBlock { Text = DiskNode.FormatBytes(nodes.Sum(n => n.SizeBytes)), Foreground = (Brush)FindResource("TextSecondaryBrush") }; Grid.SetColumn(sum, 1); head.Children.Add(sum);
        panel.Children.Add(head);
        foreach (var n in nodes)
        {
            var g = new Grid { Margin = new Thickness(12, 4, 12, 6) };
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); g.ColumnDefinitions.Add(new ColumnDefinition()); g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var ic = MakeIcon(n.Path, 26); ic.Margin = new Thickness(0, 0, 10, 0); g.Children.Add(ic);
            var t = new StackPanel(); t.Children.Add(new TextBlock { Text = n.Name, FontWeight = FontWeights.Medium });
            var status = res is not null && res.TryGetValue(n.Path, out var o) ? (o.Moved ? L.X("mutat în Coș", "moved to the Recycle Bin", "movido a la Papelera") : L.X("eșuat: ", "failed: ", "error: ") + o.Error) : System.IO.Path.GetDirectoryName(n.Path) ?? "";
            t.Children.Add(new TextBlock { Text = status, FontSize = 11.5, TextTrimming = TextTrimming.CharacterEllipsis, Foreground = res is not null && res.TryGetValue(n.Path, out var o2) && !o2.Moved ? (Brush)FindResource("StatusErrorBrush") : (Brush)FindResource("TextSecondaryBrush") });
            Grid.SetColumn(t, 1); g.Children.Add(t);
            var sz = new TextBlock { Text = DiskNode.FormatBytes(n.SizeBytes), Foreground = (Brush)FindResource("TextSecondaryBrush"), VerticalAlignment = VerticalAlignment.Center }; Grid.SetColumn(sz, 2); g.Children.Add(sz);
            panel.Children.Add(g);
        }
        return new Border { Child = panel, CornerRadius = new CornerRadius(12), BorderThickness = new Thickness(risk ? 1.5 : 1), BorderBrush = risk ? (Brush)FindResource("StatusErrorBrush") : (Brush)FindResource("CardStrokeBrush"), Background = (Brush)FindResource("CardBackgroundBrush"), Margin = new Thickness(0, 0, 0, 12) };
    }

    private void OnSheetRun(object sender, RoutedEventArgs e)
    {
        if (!LicenseManager.Shared.IsUnlocked) { new AccessGateWindow { Owner = Window.GetWindow(this) }.ShowDialog(); return; }
        if (_trashing) return;
        var s = SpaceMath.Summarize(_selection.Values, NoDuplicates);
        var wanted = new HashSet<string>(s.Recoverable.Concat(_confirmUnverified ? s.Unverified : Enumerable.Empty<string>()), StringComparer.OrdinalIgnoreCase);
        var items = _selection.Values.Where(n => wanted.Contains(n.Path)).ToList();
        if (items.Count == 0) return;
        _trashing = true; _resultItems = items; RenderSheet();
        var op = DiagnosticLog.Begin("trash", "move");
        Task.Run(() =>
        {
            var outcomes = new Dictionary<string, RecycleBinService.Outcome>(StringComparer.OrdinalIgnoreCase);
            foreach (var n in items) outcomes[n.Path] = RecycleBinService.MoveToRecycleBin(n.Path);
            Dispatcher.Invoke(() =>
            {
                op.End(outcomes.Values.All(o => o.Moved) ? "ok" : "partial", outcomes.Values.All(o => o.Moved) ? "info" : "warning");
                if (_tree is not null && _currentRootPath is not null)
                {
                    foreach (var (path, o) in outcomes.Where(kv => kv.Value.Moved)) { DiskScanEngine.Remove(path, _tree); _selection.Remove(path); }
                    DiskCacheStore.Save(new DiskCacheStore.Snapshot { RootPath = _currentRootPath, ScannedAtUtc = _lastScannedAtUtc ?? DateTime.UtcNow, Root = _tree, AccessErrors = _accessErrors, VerifiedAtUtc = _verifiedAtUtc });
                }
                _results = outcomes; _trashing = false; _totals = null; RenderResult();
            });
        });
    }
}
