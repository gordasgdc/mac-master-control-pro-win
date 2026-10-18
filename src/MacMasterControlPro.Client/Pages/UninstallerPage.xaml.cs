using System.Windows;
using System.Windows.Controls;
using MacMasterControlPro.Core.Services;

namespace MacMasterControlPro.Client.Pages;

public partial class UninstallerPage : UserControl
{
    private List<InstalledAppWin> _apps = new();
    private InstalledAppWin? _selectedApp;
    private List<UninstallCategoryWin> _categories = new();
    private readonly HashSet<string> _selectedCategoryIds = new();
    private readonly Controls.TerminalLogView _log = new();

    /// Selecție separată de "aplicația deschisă în detaliu" — cerință
    /// directă (2026-09-01): "vreau sa pot selecta mai multe si sa le
    /// dezinstalez, nu una cate una". Bifa de pe fiecare rând adaugă
    /// aplicația la ștergerea în masă; click pe numele ei tot deschide
    /// detaliul individual, ca înainte.
    private readonly HashSet<string> _bulkSelected = new();
    private bool _isBulkBusy;

    public UninstallerPage()
    {
        InitializeComponent();
        _apps = UninstallerService.ScanInstalledApps();
        RenderAppsList();
    }

    private void OnSearchChanged(object sender, TextChangedEventArgs e) => RenderAppsList();

    private void RenderAppsList()
    {
        var filter = SearchBox.Text?.Trim() ?? "";
        var filtered = _apps
            .Where(a => filter.Length == 0 || a.DisplayName.Contains(filter, StringComparison.OrdinalIgnoreCase))
            .ToList();

        AppsPanel.Children.Clear();
        foreach (var app in filtered)
        {
            var row = new Grid { Margin = new Thickness(0, 2, 0, 2) };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var check = new CheckBox
            {
                IsChecked = _bulkSelected.Contains(app.DisplayName),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 6, 0),
                ToolTip = L.X($"Bifează ca să incluzi {app.DisplayName} la o ștergere în masă a mai multor aplicații deodată.", $"Tick to include {app.DisplayName} in a bulk removal of several apps at once.", $"Marca para incluir {app.DisplayName} en una eliminación múltiple de varias apps a la vez."),
            };
            check.Checked += (_, _) => { _bulkSelected.Add(app.DisplayName); UpdateBulkButton(); };
            check.Unchecked += (_, _) => { _bulkSelected.Remove(app.DisplayName); UpdateBulkButton(); };
            Grid.SetColumn(check, 0);
            row.Children.Add(check);

            var name = new TextBlock
            {
                Text = app.DisplayName,
                VerticalAlignment = VerticalAlignment.Center,
                TextTrimming = TextTrimming.CharacterEllipsis,
                Cursor = System.Windows.Input.Cursors.Hand,
            };
            name.MouseLeftButtonUp += (_, _) => SelectApp(app);
            Grid.SetColumn(name, 1);
            row.Children.Add(name);

            AppsPanel.Children.Add(row);
        }
    }

    private void UpdateBulkButton()
    {
        BulkDeleteButton.Visibility = _bulkSelected.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        BulkDeleteButton.Content = L.X($"Dezinstalează selectate ({_bulkSelected.Count})", $"Uninstall selected ({_bulkSelected.Count})", $"Desinstalar seleccionadas ({_bulkSelected.Count})");
    }

    private void SelectApp(InstalledAppWin app)
    {
        _selectedApp = app;
        _categories = UninstallerService.ScanRelatedFiles(app);
        _selectedCategoryIds.Clear();
        foreach (var c in _categories) _selectedCategoryIds.Add(c.Id);
        RenderDetails();
    }

    private void RenderDetails()
    {
        if (_selectedApp is null) return;
        DetailsPanel.Children.Clear();

        DetailsPanel.Children.Add(new TextBlock { Text = _selectedApp.DisplayName, FontSize = 18, FontWeight = FontWeights.Bold, Margin = new Thickness(0, 0, 0, 12) });

        if (_categories.Count == 0)
        {
            DetailsPanel.Children.Add(new TextBlock { Text = L.X("Nicio urmă găsită în afara aplicației înseși.", "No leftovers found outside the app itself.", "No se encontraron restos fuera de la propia app."), Foreground = System.Windows.Media.Brushes.Gray });
        }
        else
        {
            foreach (var category in _categories)
            {
                var row = new Grid { Margin = new Thickness(0, 3, 0, 3) };
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

                var check = new CheckBox
                {
                    Content = category.RequiresPrivilege ? $"{category.Title} 🔒" : category.Title,
                    IsChecked = _selectedCategoryIds.Contains(category.Id),
                };
                check.Checked += (_, _) => _selectedCategoryIds.Add(category.Id);
                check.Unchecked += (_, _) => _selectedCategoryIds.Remove(category.Id);
                Grid.SetColumn(check, 0);
                row.Children.Add(check);

                var size = new TextBlock { Text = category.SizeDescription, Foreground = System.Windows.Media.Brushes.Gray, FontSize = 11 };
                Grid.SetColumn(size, 1);
                row.Children.Add(size);

                DetailsPanel.Children.Add(row);
            }
        }

        var deleteButton = new Wpf.Ui.Controls.Button
        {
            Content = L.X("Șterge selectate", "Delete selected", "Eliminar seleccionados"),
            Appearance = Wpf.Ui.Controls.ControlAppearance.Danger,
            Margin = new Thickness(0, 12, 0, 8),
            ToolTip = L.X($"Șterge doar categoriile bifate mai sus pentru {_selectedApp.DisplayName} — folosește asta ca să păstrezi ceva anume.", $"Deletes only the categories ticked above for {_selectedApp.DisplayName} — use this to keep something specific.", $"Elimina solo las categorías marcadas arriba para {_selectedApp.DisplayName}; úsalo para conservar algo concreto."),
        };
        deleteButton.Click += (_, _) => PerformDelete();
        DetailsPanel.Children.Add(deleteButton);
        DetailsPanel.Children.Add(_log);
    }

    private void PerformDelete()
    {
        if (_selectedApp is null || !RequireLicense()) return;
        _log.Clear();
        _log.Append(L.X($"Încep ștergerea urmelor pentru {_selectedApp.DisplayName}…", $"Starting leftover removal for {_selectedApp.DisplayName}…", $"Iniciando la eliminación de restos de {_selectedApp.DisplayName}…"));
        UninstallerService.TerminateIfRunning(_selectedApp.InstallLocation);
        var toDelete = _categories.Where(c => _selectedCategoryIds.Contains(c.Id)).ToList();
        UninstallerService.UninstallBatch(new[] { new UninstallerService.Target(_selectedApp, toDelete) }, line => _log.Append(line));
        _log.Append(L.X("Gata.", "Done.", "Listo."));
        _apps = UninstallerService.ScanInstalledApps();
        RenderAppsList();
    }

    /// Ștergere în masă — cerință directă (2026-09-01): "vreau sa pot
    /// selecta mai multe si sa le dezinstalez, nu una cate una". Fiecare
    /// aplicație bifată e scanată din nou chiar înainte de ștergere și
    /// ștearsă COMPLET (toate categoriile găsite + dezinstalatorul
    /// oficial), la fel de riguros ca fluxul individual.
    private void OnBulkDeleteClicked(object sender, RoutedEventArgs e)
    {
        if (_isBulkBusy || !RequireLicense()) return;
        var targets = _apps.Where(a => _bulkSelected.Contains(a.DisplayName)).ToList();
        if (targets.Count == 0) return;

        var confirm = System.Windows.MessageBox.Show(
            L.X($"Ștergi definitiv {targets.Count} aplicații, cu toate urmele lor?", $"Permanently delete {targets.Count} apps, with all their leftovers?", $"¿Eliminar definitivamente {targets.Count} apps, con todos sus restos?"),
            "Confirmare", MessageBoxButton.YesNo, MessageBoxImage.Warning);
        if (confirm != MessageBoxResult.Yes) return;

        _isBulkBusy = true;
        _log.Clear();
        _log.Append(L.X($"Încep ștergerea în masă pentru {targets.Count} aplicații…", $"Starting bulk removal of {targets.Count} apps…", $"Iniciando la eliminación múltiple de {targets.Count} apps…"));
        DetailsPanel.Children.Clear();
        DetailsPanel.Children.Add(_log);

        Task.Run(() =>
        {
            var plan = new List<UninstallerService.Target>();
            foreach (var app in targets)
            {
                UninstallerService.TerminateIfRunning(app.InstallLocation);
                plan.Add(new UninstallerService.Target(app, UninstallerService.ScanRelatedFiles(app)));
            }
            UninstallerService.UninstallBatch(plan, line => Dispatcher.Invoke(() => _log.Append(line)));
            Dispatcher.Invoke(() =>
            {
                _log.Append(L.X("Gata.", "Done.", "Listo."));
                _apps = UninstallerService.ScanInstalledApps();
                _bulkSelected.Clear();
                _selectedApp = null;
                RenderAppsList();
                UpdateBulkButton();
                _isBulkBusy = false;
            });
        });
    }

    private bool RequireLicense()
    {
        if (LicenseManager.Shared.IsUnlocked) return true;
        var gate = new AccessGateWindow { Owner = Window.GetWindow(this) };
        return gate.ShowDialog() == true;
    }
}
