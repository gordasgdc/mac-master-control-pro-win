using System.Windows;
using System.Windows.Controls;
using MacMasterControlPro.Core.Services;

namespace MacMasterControlPro.Client.Pages;

public partial class WindowLayoutsPage : UserControl
{
    private List<(string name, string processName, int pid)> _apps = new();

    public WindowLayoutsPage()
    {
        InitializeComponent();
        Refresh();
    }

    private void Refresh()
    {
        _apps = WindowLayoutService.RunningApps();
        AppCombo.ItemsSource = _apps;
        RenderProfiles();
    }

    private void RenderProfiles()
    {
        ProfilesPanel.Children.Clear();
        var profiles = WindowLayoutService.AllProfiles();
        if (profiles.Count == 0)
        {
            ProfilesPanel.Children.Add(new TextBlock { Text = L.X("Niciun profil salvat încă.", "No profile saved yet.", "Aún no hay ningún perfil guardado."), Foreground = System.Windows.Media.Brushes.Gray, Margin = new Thickness(0, 12, 0, 12) });
            return;
        }
        foreach (var profile in profiles)
        {
            var row = new Grid { Margin = new Thickness(0, 4, 0, 4) };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var labelPanel = new StackPanel();
            labelPanel.Children.Add(new TextBlock { Text = profile.Name, FontWeight = FontWeights.Bold });
            labelPanel.Children.Add(new TextBlock { Text = $"{profile.Frames.Count} ferestre · {profile.SavedAt:g}", FontSize = 10, Foreground = System.Windows.Media.Brushes.Gray });
            Grid.SetColumn(labelPanel, 0);
            row.Children.Add(labelPanel);

            var restoreButton = new Wpf.Ui.Controls.Button { Content = L.X("Restaurează", "Restore", "Restaurar"), Margin = new Thickness(0, 0, 6, 0) };
            restoreButton.Click += (_, _) => Restore(profile);
            Grid.SetColumn(restoreButton, 1);
            row.Children.Add(restoreButton);

            var deleteButton = new Wpf.Ui.Controls.Button { Content = "🗑" };
            deleteButton.Click += (_, _) => { WindowLayoutService.DeleteProfile(profile.Name); RenderProfiles(); };
            Grid.SetColumn(deleteButton, 2);
            row.Children.Add(deleteButton);

            ProfilesPanel.Children.Add(row);
        }
    }

    private void OnSaveClicked(object sender, RoutedEventArgs e)
    {
        if (!RequireLicense()) return;
        if (AppCombo.SelectedItem is not (string name, string processName, int pid)) return;
        var profileName = ProfileNameBox.Text.Trim();
        if (string.IsNullOrEmpty(profileName)) return;
        var ok = WindowLayoutService.SaveLayout(profileName, processName, pid);
        StatusText.Text = ok ? $"✔ Profil „{profileName}” salvat." : L.X("✘ Nu s-au putut citi ferestrele.", "✘ Could not read the windows.", "✘ No se pudieron leer las ventanas.");
        ProfileNameBox.Text = "";
        Refresh();
    }

    private void Restore(WindowLayoutProfile profile)
    {
        if (!RequireLicense()) return;
        var app = _apps.FirstOrDefault(a => a.processName == profile.ProcessName);
        if (app.processName is null) { StatusText.Text = L.X($"✘ Aplicația „{profile.ProcessName}” nu rulează acum.", $"✘ The app “{profile.ProcessName}” is not running now.", $"✘ La app «{profile.ProcessName}» no se está ejecutando ahora."); return; }
        var count = WindowLayoutService.RestoreLayout(profile, app.pid);
        StatusText.Text = L.X($"✔ {count} ferestre repoziționate din profilul „{profile.Name}”.", $"✔ {count} windows repositioned from profile “{profile.Name}”.", $"✔ {count} ventanas reposicionadas desde el perfil «{profile.Name}».");
    }

    private bool RequireLicense()
    {
        if (LicenseManager.Shared.IsUnlocked) return true;
        var gate = new AccessGateWindow { Owner = Window.GetWindow(this) };
        return gate.ShowDialog() == true;
    }
}
