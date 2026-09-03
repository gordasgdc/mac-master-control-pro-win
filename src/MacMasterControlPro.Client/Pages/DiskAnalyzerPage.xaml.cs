using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using MacMasterControlPro.Core.Services;

namespace MacMasterControlPro.Client.Pages;

/// Port 1:1 al DiskAnalyzerView.swift/DiskAnalyzerViewModel.swift (Mac,
/// v2.30.0) — nu exista deloc pe Windows inainte de asta (doar
/// DiskHealthPage, sanatate SMART, complet diferit). Stare in code-behind,
/// nu ViewModel separat — acelasi tipar ca DuplicateFinderPage/CleanupPage
/// din acest repo, nu o abatere.
public partial class DiskAnalyzerPage : UserControl
{
    private static readonly Color[] Palette =
    {
        Color.FromRgb(0xF5, 0xA6, 0x23), Color.FromRgb(0x3D, 0xC9, 0xE8), Color.FromRgb(0xB1, 0x8C, 0xF2),
        Color.FromRgb(0x5F, 0xC8, 0x5F),
        Color.FromRgb(0xF0, 0x6F, 0x9E), Color.FromRgb(0xE8, 0xD4, 0x4F), Color.FromRgb(0x7C, 0x8B, 0xF0),
        Color.FromRgb(0x6F, 0xD9, 0xC4), Color.FromRgb(0x8E, 0xB0, 0xC2), Color.FromRgb(0xC2, 0x9A, 0x7A),
    };

    private DiskNode? _tree;
    private readonly List<DiskNode> _pathStack = new();
    private string? _currentRootPath;
    private DateTime? _lastScannedAtUtc;
    private bool _isIndexing;
    private bool _isRescanning;

    private DiskNode? CurrentNode => _pathStack.Count > 0 ? _pathStack[^1] : _tree;
    private List<DiskNode> CurrentChildren => CurrentNode?.SortedChildren.ToList() ?? new();

    public DiskAnalyzerPage()
    {
        InitializeComponent();
        RenderRootPicker();
    }

    // MARK: - Alegere radacina / cache

    private void RenderRootPicker()
    {
        RootsList.Children.Clear();
        foreach (var root in DiskAnalyzerServiceRoots.AvailableRoots())
        {
            var button = new Wpf.Ui.Controls.Button
            {
                Content = $"💽  {root.Name}          {root.SizeDescription}",
                HorizontalContentAlignment = HorizontalAlignment.Left,
                Margin = new Thickness(0, 0, 0, 6),
                HorizontalAlignment = HorizontalAlignment.Stretch,
            };
            button.Click += (_, _) => StartIndexing(root);
            RootsList.Children.Add(button);
        }
        if (RootsList.Children.Count == 0)
        {
            RootsList.Children.Add(new TextBlock { Text = "Nicio unitate detectată.", Foreground = Brushes.Gray, FontSize = 11 });
        }
    }

    /// Alege o radacina — incarca INSTANT cache-ul salvat, daca exista,
    /// altfel porneste o scanare completa.
    private void StartIndexing(DiskEntry root)
    {
        _currentRootPath = root.Path;
        _pathStack.Clear();
        HideDeleteError();

        var cached = DiskCacheStore.Load(root.Path);
        if (cached is not null)
        {
            _tree = cached.Root;
            _lastScannedAtUtc = cached.ScannedAtUtc;
            RenderResult();
            return;
        }
        PerformFullScan(root.Path);
    }

    private void PerformFullScan(string rootPath)
    {
        _tree = null;
        _isIndexing = true;
        RenderState();
        IndexingStatusText.Text = "Indexez… 0 fișiere până acum.";

        Task.Run(() =>
        {
            var node = DiskScanEngine.BuildTree(rootPath, progress =>
                Dispatcher.Invoke(() => IndexingStatusText.Text =
                    $"Indexez… {progress.FilesIndexed:N0} fișiere, {DiskNode.FormatBytes(progress.BytesIndexed)} până acum."));

            Dispatcher.Invoke(() =>
            {
                _tree = node;
                _isIndexing = false;
                _lastScannedAtUtc = DateTime.UtcNow;
                DiskCacheStore.Save(new DiskCacheStore.Snapshot { RootPath = rootPath, ScannedAtUtc = _lastScannedAtUtc.Value, Root = node });
                RenderResult();
            });
        });
    }

    /// „Re-scanează doar modificările" — compară cache-ul cu discul,
    /// atinge doar ce s-a schimbat. Arborele existent rămâne navigabil
    /// normal cât timp rulează.
    private void OnRescanClicked(object sender, RoutedEventArgs e)
    {
        if (_tree is null || _currentRootPath is null || _isRescanning || _isIndexing) return;
        _isRescanning = true;
        RenderCacheBanner();

        var rootPath = _currentRootPath;
        Task.Run(() =>
        {
            var node = DiskScanEngine.IncrementalUpdate(_tree, _ => { }); // progres per-fisier nu are sens aici - de regula dureaza cateva secunde
            Dispatcher.Invoke(() =>
            {
                _tree = node;
                _isRescanning = false;
                _lastScannedAtUtc = DateTime.UtcNow;
                DiskCacheStore.Save(new DiskCacheStore.Snapshot { RootPath = rootPath, ScannedAtUtc = _lastScannedAtUtc.Value, Root = node });
                RenderResult();
            });
        });
    }

    /// „Resetare Cache & Scanare Completă" — cerut explicit doar pentru
    /// cazurile rare, la fel ca pe Mac.
    private void OnResetCacheClicked(object sender, RoutedEventArgs e)
    {
        if (_currentRootPath is null) return;
        var confirm = System.Windows.MessageBox.Show(
            "Poate dura mult pe un disc mare — folosește-l doar dacă analiza pare vizibil greșită. „Re-scanează doar modificările” e suficient în mod normal.\n\nResetezi cache-ul și rescanezi tot discul de la zero?",
            "Confirmare", MessageBoxButton.YesNo, MessageBoxImage.Warning);
        if (confirm != MessageBoxResult.Yes) return;

        DiskCacheStore.Clear(_currentRootPath);
        _pathStack.Clear();
        _lastScannedAtUtc = null;
        PerformFullScan(_currentRootPath);
    }

    private void OnRootIconClicked(object sender, RoutedEventArgs e)
    {
        _tree = null;
        _pathStack.Clear();
        _isIndexing = false;
        _isRescanning = false;
        _lastScannedAtUtc = null;
        _currentRootPath = null;
        RenderRootPicker();
        RenderState();
    }

    // MARK: - Randare

    private void RenderState()
    {
        RootPickerPanel.Visibility = (!_isIndexing && _tree is null) ? Visibility.Visible : Visibility.Collapsed;
        IndexingPanel.Visibility = _isIndexing ? Visibility.Visible : Visibility.Collapsed;
        ResultPanel.Visibility = (!_isIndexing && _tree is not null) ? Visibility.Visible : Visibility.Collapsed;
    }

    private void RenderResult()
    {
        RenderState();
        if (_tree is null) return;
        RenderCacheBanner();
        RenderBreadcrumb();
        RenderChildren();
    }

    private void RenderCacheBanner()
    {
        if (_lastScannedAtUtc is null) { CacheBanner.Visibility = Visibility.Collapsed; return; }
        CacheBanner.Visibility = Visibility.Visible;
        CacheBannerText.Text = $"Afișez datele din analiza de la {_lastScannedAtUtc.Value.ToLocalTime():d MMMM, HH:mm}.";
        RescanSpinner.Visibility = _isRescanning ? Visibility.Visible : Visibility.Collapsed;
        RescanningText.Visibility = _isRescanning ? Visibility.Visible : Visibility.Collapsed;
        RescanButton.Visibility = _isRescanning ? Visibility.Collapsed : Visibility.Visible;
        ResetCacheButton.Visibility = _isRescanning ? Visibility.Collapsed : Visibility.Visible;
    }

    private void RenderBreadcrumb()
    {
        BreadcrumbPanel.Children.Clear();
        for (int i = 0; i < _pathStack.Count; i++)
        {
            var index = i;
            BreadcrumbPanel.Children.Add(new TextBlock { Text = " › ", Foreground = Brushes.Gray, VerticalAlignment = VerticalAlignment.Center });
            var crumb = new Wpf.Ui.Controls.Button
            {
                Content = _pathStack[i].Name,
                Appearance = Wpf.Ui.Controls.ControlAppearance.Transparent,
                FontWeight = i == _pathStack.Count - 1 ? FontWeights.SemiBold : FontWeights.Normal,
            };
            crumb.Click += (_, _) => { _pathStack.RemoveRange(index + 1, _pathStack.Count - index - 1); RenderResult(); };
            BreadcrumbPanel.Children.Add(crumb);
        }
    }

    private void RenderChildren()
    {
        var children = CurrentChildren;
        EmptyFolderText.Visibility = children.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        ProportionalBarHost.Visibility = children.Count == 0 ? Visibility.Collapsed : Visibility.Visible;

        // Bara proportionala - un Grid cu coloane Star, proportionale cu
        // marimea fiecarei intrari; WPF distribuie automat latimea
        // disponibila intre coloane Star, la fel ca frame(width:) calculat
        // manual pe Mac, fara nicio matematica de pixeli aici.
        ProportionalBar.Children.Clear();
        ProportionalBar.ColumnDefinitions.Clear();
        var top = children.Take(30).ToList();
        for (int i = 0; i < top.Count; i++)
        {
            var fraction = Math.Max(top[i].SizeBytes, 1);
            ProportionalBar.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(fraction, GridUnitType.Star) });
            var bar = new Border
            {
                Background = new SolidColorBrush(Palette[i % Palette.Length]),
                Margin = new Thickness(i == 0 ? 0 : 1, 0, 0, 0),
                ToolTip = $"{top[i].Name} — {top[i].SizeDescription}",
                Cursor = System.Windows.Input.Cursors.Hand,
            };
            var node = top[i];
            bar.MouseLeftButtonUp += (_, _) => OpenNode(node);
            Grid.SetColumn(bar, i);
            ProportionalBar.Children.Add(bar);
        }

        EntryList.Children.Clear();
        for (int i = 0; i < children.Count; i++)
        {
            EntryList.Children.Add(BuildEntryRow(children[i], Palette[i % Palette.Length]));
            if (i < children.Count - 1) EntryList.Children.Add(new Separator { Margin = new Thickness(0, 2, 0, 2), Opacity = 0.15 });
        }
    }

    private UIElement BuildEntryRow(DiskNode node, Color dotColor)
    {
        var row = new Grid { Margin = new Thickness(0, 6, 0, 6), Cursor = System.Windows.Input.Cursors.Hand };
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var dot = new Ellipse { Width = 8, Height = 8, Fill = new SolidColorBrush(dotColor), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 8, 0) };
        Grid.SetColumn(dot, 0);
        row.Children.Add(dot);

        var name = new TextBlock
        {
            Text = (node.IsDirectory ? "📁 " : "📄 ") + node.Name,
            TextTrimming = TextTrimming.CharacterEllipsis,
            VerticalAlignment = VerticalAlignment.Center,
            ToolTip = node.Path,
        };
        Grid.SetColumn(name, 1);
        row.Children.Add(name);

        var size = new TextBlock { Text = node.SizeDescription, Foreground = Brushes.Gray, FontFamily = new FontFamily("Consolas"), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(10, 0, 10, 0) };
        Grid.SetColumn(size, 2);
        row.Children.Add(size);

        var openButton = new Wpf.Ui.Controls.Button
        {
            Content = node.IsDirectory ? "📂" : "↗️",
            Appearance = Wpf.Ui.Controls.ControlAppearance.Transparent,
            Padding = new Thickness(6, 0, 6, 0),
            ToolTip = node.IsDirectory ? "Deschide folderul în Explorer" : "Arată în Explorer",
        };
        openButton.Click += (_, _) => OpenInExplorer(node);
        Grid.SetColumn(openButton, 3);
        row.Children.Add(openButton);

        var deleteButton = new Wpf.Ui.Controls.Button
        {
            Content = "🗑️",
            Appearance = Wpf.Ui.Controls.ControlAppearance.Transparent,
            Padding = new Thickness(6, 0, 6, 0),
            ToolTip = "Șterge (mută la Coșul de reciclare)",
        };
        deleteButton.Click += (_, _) => ConfirmAndDelete(node);
        Grid.SetColumn(deleteButton, 4);
        row.Children.Add(deleteButton);

        row.MouseLeftButtonUp += (_, e) =>
        {
            if (e.OriginalSource == openButton || e.OriginalSource == deleteButton) return;
            OpenNode(node);
        };
        return row;
    }

    private void OpenNode(DiskNode node)
    {
        if (!node.IsDirectory) return;
        _pathStack.Add(node);
        RenderResult();
    }

    /// Pentru foldere, deschide o fereastra Explorer NAVIGATA DIRECT in
    /// acel folder (echivalentul Windows al NSWorkspace.open pe Mac) -
    /// arata efectiv ce e inauntru, nu doar il evidentiaza in parinte.
    /// Pentru fisiere, ramane comportamentul de reveal (`/select,`).
    private static void OpenInExplorer(DiskNode node)
    {
        try
        {
            if (node.IsDirectory) Process.Start("explorer.exe", $"\"{node.Path}\"");
            else Process.Start("explorer.exe", $"/select,\"{node.Path}\"");
        }
        catch { /* folder/fisier posibil disparut intre timp */ }
    }

    // MARK: - Stergere

    private void ConfirmAndDelete(DiskNode node)
    {
        if (!RequireLicense()) return;
        var confirm = System.Windows.MessageBox.Show(
            $"Muți „{node.Name}” ({node.SizeDescription}) la Coșul de reciclare?\n\nDacă permisiunile refuză, se cere automat rularea ca administrator.",
            "Confirmare", MessageBoxButton.YesNo, MessageBoxImage.Warning);
        if (confirm != MessageBoxResult.Yes) return;

        HideDeleteError();
        var nodePath = node.Path;
        Task.Run(() =>
        {
            var error = PrivilegedFileOps.Delete(nodePath);
            Dispatcher.Invoke(() =>
            {
                if (error is not null)
                {
                    ShowDeleteError(error);
                    return;
                }
                if (_tree is not null && _currentRootPath is not null)
                {
                    DiskScanEngine.Remove(nodePath, _tree);
                    // Salveaza cache-ul actualizat - altfel, la urmatoarea
                    // deschidere a aplicatiei, cache-ul VECHI ar arata din
                    // nou fisierul/folderul deja sters.
                    DiskCacheStore.Save(new DiskCacheStore.Snapshot { RootPath = _currentRootPath, ScannedAtUtc = _lastScannedAtUtc ?? DateTime.UtcNow, Root = _tree });
                    RenderResult();
                }
            });
        });
    }

    private void ShowDeleteError(string error)
    {
        DeleteErrorText.Text = $"✘ Nu s-a putut șterge: {error}";
        DeleteErrorText.Visibility = Visibility.Visible;
    }

    private void HideDeleteError() => DeleteErrorText.Visibility = Visibility.Collapsed;

    private bool RequireLicense()
    {
        if (LicenseManager.Shared.IsUnlocked) return true;
        var gate = new TrialGateWindow { Owner = Window.GetWindow(this) };
        return gate.ShowDialog() == true;
    }
}
