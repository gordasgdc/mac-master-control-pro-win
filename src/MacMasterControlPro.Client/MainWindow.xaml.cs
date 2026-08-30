using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using MacMasterControlPro.Client.Pages;
using MacMasterControlPro.Client.Services;
using MacMasterControlPro.Core.Services;

namespace MacMasterControlPro.Client;

public partial class MainWindow
{
    private readonly DependencyChecker _dependencyChecker = new();

    public MainWindow()
    {
        InitializeComponent();
        ApplyTextScale(TextScaleStore.Load());

        VersionText.Text = $"v{UpdateChecker.CurrentVersion} Pro";
        RefreshProfile();
        MachineIdButton.Content = $"Machine ID: {MachineID.Display[..Math.Min(13, MachineID.Display.Length)]}…";

        _dependencyChecker.CheckAll();
        RefreshDependencyBadge();

        PageHost.Content = new DashboardPage();

        _ = MaybeShowUpdatePopupAsync(respectDismissal: true);
    }

    private void OnModuleSelected(object sender, SelectionChangedEventArgs e)
    {
        if (ModuleList.SelectedItem is not ListBoxItem item) return;
        PageHost.Content = (item.Tag as string) switch
        {
            "Network" => new NetworkPage(),
            "Cloud" => new Pages.CloudPage(),
            "Cleanup" => new Pages.CleanupPage(),
            "Tweaks" => new Pages.TweaksPage(),
            "Dependencies" => new Pages.DependenciesPage(_dependencyChecker, this),
            "Settings" => new SettingsPage(this),
            _ => new DashboardPage(),
        };
    }

    public void RefreshDependencyBadge()
    {
        DependenciesItem.Foreground = _dependencyChecker.AllInstalled
            ? System.Windows.Media.Brushes.White
            : System.Windows.Media.Brushes.OrangeRed;
    }

    public void RefreshProfile()
    {
        var name = UserProfileStore.Name;
        ProfileNameText.Text = string.IsNullOrWhiteSpace(name) ? "Anonim" : name;
        ProfileEmailText.Text = UserProfileStore.Email;
    }

    private void OnCopyMachineId(object sender, RoutedEventArgs e)
    {
        Clipboard.SetText(MachineID.Display);
        MachineIdButton.Content = "Copiat ✓";
    }

    private async void OnCheckUpdatesClicked(object sender, RoutedEventArgs e)
    {
        await MaybeShowUpdatePopupAsync(respectDismissal: false);
    }

    private async Task MaybeShowUpdatePopupAsync(bool respectDismissal)
    {
        await UpdateChecker.Shared.CheckAsync();
        var version = UpdateChecker.Shared.AvailableVersion;
        if (version is null)
        {
            if (!respectDismissal)
            {
                await new Wpf.Ui.Controls.MessageBox
                {
                    Title = "Ești la zi",
                    Content = $"Rulezi deja ultima versiune ({UpdateChecker.CurrentVersion}).",
                }.ShowDialogAsync();
            }
            return;
        }
        if (respectDismissal && UpdateChecker.Shared.WasDismissed(version)) return;

        var box = new Wpf.Ui.Controls.MessageBox
        {
            Title = "Este disponibilă o versiune nouă",
            Content = $"Mac Master Control Pro {version} este disponibil (tu ai {UpdateChecker.CurrentVersion}).",
            PrimaryButtonText = "Actualizează acum",
            CloseButtonText = "Mai târziu",
        };
        var result = await box.ShowDialogAsync();
        UpdateChecker.Shared.Dismiss();
        if (result == Wpf.Ui.Controls.MessageBoxResult.Primary)
        {
            await SelfUpdater.DownloadAndInstallAsync(version);
        }
    }

    /// Regula 24 - LayoutTransform pe RootGrid, nu FontSize brut per control.
    public void ApplyTextScale(TextScalePreference preference)
    {
        var scale = preference.ScaleFactor();
        RootGrid.LayoutTransform = scale == 1.0 ? Transform.Identity : new ScaleTransform(scale, scale);
    }
}
