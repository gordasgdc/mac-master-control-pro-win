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
        AppsList.ItemsSource = _apps
            .Where(a => filter.Length == 0 || a.DisplayName.Contains(filter, StringComparison.OrdinalIgnoreCase))
            .Select(a => a.DisplayName)
            .ToList();
    }

    private void OnAppSelected(object sender, SelectionChangedEventArgs e)
    {
        if (AppsList.SelectedItem is not string name) return;
        _selectedApp = _apps.FirstOrDefault(a => a.DisplayName == name);
        if (_selectedApp is null) return;
        _categories = UninstallerService.ScanRelatedFiles(_selectedApp);
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
            DetailsPanel.Children.Add(new TextBlock { Text = "Nicio urmă găsită în afara aplicației înseși.", Foreground = System.Windows.Media.Brushes.Gray });
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

        var deleteButton = new Wpf.Ui.Controls.Button { Content = "Șterge selectate", Appearance = Wpf.Ui.Controls.ControlAppearance.Danger, Margin = new Thickness(0, 12, 0, 8) };
        deleteButton.Click += (_, _) => PerformDelete();
        DetailsPanel.Children.Add(deleteButton);
        DetailsPanel.Children.Add(_log);
    }

    private void PerformDelete()
    {
        if (_selectedApp is null || !RequireLicense()) return;
        _log.Clear();
        _log.Append($"Încep ștergerea urmelor pentru {_selectedApp.DisplayName}…");
        var toDelete = _categories.Where(c => _selectedCategoryIds.Contains(c.Id)).ToList();
        UninstallerService.Delete(toDelete, line => _log.Append(line));

        if (!string.IsNullOrWhiteSpace(_selectedApp.UninstallString))
        {
            _log.Append("Rulez dezinstalatorul oficial (Apps & Features)…");
            var ok = UninstallerService.RunOfficialUninstaller(_selectedApp);
            _log.Append(ok ? "✔ Dezinstalator oficial rulat." : "EROARE la rularea dezinstalatorului oficial.");
        }
        _log.Append("Gata.");
        _apps = UninstallerService.ScanInstalledApps();
        RenderAppsList();
    }

    private bool RequireLicense()
    {
        if (LicenseManager.Shared.IsUnlocked) return true;
        var gate = new TrialGateWindow { Owner = Window.GetWindow(this) };
        return gate.ShowDialog() == true;
    }
}
