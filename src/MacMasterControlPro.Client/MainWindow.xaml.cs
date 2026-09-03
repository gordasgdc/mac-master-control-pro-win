using System.Collections.Generic;
using System.Linq;
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
        RefreshLicenseBadge();
        MachineIdButton.Content = $"Machine ID: {MachineID.Display[..Math.Min(13, MachineID.Display.Length)]}…";

        _dependencyChecker.CheckAll();
        RefreshDependencyBadge();

        RenderSidebarLabels();
        LanguageStore.Changed += RenderSidebarLabels;
        LanguageStore.Changed += () => { if (PageHost.Content is DashboardPage) PageHost.Content = new DashboardPage(); };

        PageHost.Content = new DashboardPage();
        // Evidentiaza vizual "Dashboard" in lista - facut aici, NU cu
        // IsSelected="True" in XAML (vezi comentariul din MainWindow.xaml):
        // la acest punct PageHost e deja asignat, deci OnModuleSelected
        // (declansat de asta) e sigur.
        ModuleList.SelectedItem = ItemDashboard;

        _ = MaybeShowUpdatePopupAsync(respectDismissal: true);
    }

    private void RenderSidebarLabels()
    {
        ItemDashboard.Content = "📊 " + L.T("sidebar.dashboard");
        ItemRenderMode.Content = "⚡️ " + L.T("sidebar.renderMode");
        ItemLoginItems.Content = "🔌 " + L.T("sidebar.loginItems");
        ItemProcessMonitor.Content = "⚙️ " + L.T("sidebar.processMonitor");
        ItemUninstaller.Content = "🗑️ " + L.T("sidebar.uninstaller");
        ItemDiskHealth.Content = "💽 " + L.T("sidebar.diskHealth");
        ItemDiskAnalyzer.Content = "📊 " + L.T("sidebar.diskAnalyzer");
        ItemResolveTools.Content = "🎬 " + L.T("sidebar.resolveTools");
        ItemWindowLayouts.Content = "🪟 " + L.T("sidebar.windowLayouts");
        ItemNetwork.Content = "🌐 " + L.T("sidebar.network");
        ItemCloud.Content = "☁️ " + L.T("sidebar.cloud");
        ItemCleanup.Content = "🧹 " + L.T("sidebar.cleanup");
        ItemDuplicates.Content = "🧬 " + L.T("sidebar.duplicates");
        ItemSecurity.Content = "🛡️ " + L.T("sidebar.security");
        ItemTweaks.Content = "🛠️ " + L.T("sidebar.tweaks");
        DependenciesItem.Content = "🧩 " + L.T("sidebar.dependencies");
        ItemSettings.Content = "⚙️ " + L.T("sidebar.settings");
        CheckUpdatesButton.Content = L.T("sidebar.checkUpdates");

        // Cerinta directa (2026-09-01): "cand te duci cu mouse-ul peste un
        // buton, sa-ti apara o descriere de ce face".
        ItemDashboard.ToolTip = "Privire de ansamblu — starea generală a PC-ului dintr-o privire.";
        ItemRenderMode.ToolTip = "Oprește temporar Windows Search/File History cât randezi în DaVinci Resolve.";
        ItemLoginItems.ToolTip = "Vezi și oprești aplicațiile care pornesc automat odată cu Windows.";
        ItemProcessMonitor.ToolTip = "Procesele active acum, sortabile după RAM — închide ce consumă prea mult.";
        ItemUninstaller.ToolTip = "Dezinstalează complet una sau mai multe aplicații, cu toate urmele lor.";
        ItemDiskHealth.ToolTip = "Spațiu liber, status SMART și test de viteză pentru discurile montate.";
        ItemDiskAnalyzer.ToolTip = "Vezi ce ocupă spațiul pe disc, folder cu folder — indexare o singură dată, apoi navigare instantă.";
        ItemResolveTools.ToolTip = "Notificare la final de randare, verificare Media Pool, sincronizare LUT-uri, backup bază de date.";
        ItemWindowLayouts.ToolTip = "Salvează și restaurează aranjamentul ferestrelor pe ecran.";
        ItemNetwork.ToolTip = "Configurare și optimizare rețea, persistentă la repornire.";
        ItemCloud.ToolTip = "Conectează și gestionează conturi Cloud (Drive, Dropbox, S3 și altele).";
        ItemCleanup.ToolTip = "Șterge cache-uri recuperabile, fișiere mari uitate, eliberează RAM.";
        ItemDuplicates.ToolTip = "Găsește fișiere identice ca și conținut și te lasă să alegi ce ștergi.";
        ItemSecurity.ToolTip = "Verifică setările de securitate ale PC-ului, cu ghid pas-cu-pas pentru ce lipsește.";
        ItemTweaks.ToolTip = "Ajustări rapide de sistem.";
        DependenciesItem.ToolTip = "Componentele externe de care aplicația are nevoie — instalare cu un click.";
        ItemSettings.ToolTip = "Temă, limbă, licență și alte preferințe ale aplicației.";
    }

    /// Cuvinte-cheie suplimentare de cautare, pe langa nume/tooltip -
    /// cerinta directa (Cristi, 2026-09-01): "sa pot cauta ... reglare,
    /// duplicate, dezinstalare ... prin toata aplicatia".
    private static readonly Dictionary<string, string> SearchKeywords = new()
    {
        ["Tweaks"] = "reglare setari sistem",
        ["Uninstaller"] = "dezinstalare sterge aplicatii",
        ["Duplicates"] = "duplicate copii identice fisiere",
        ["Cleanup"] = "curatare ram cache fisiere mari",
        ["DiskHealth"] = "disc viteza smart",
        ["DiskAnalyzer"] = "analiza disc spatiu ocupat folder mare daisydisk treesize",
        ["Security"] = "securitate firewall defender",
        ["Cloud"] = "cloud drive dropbox onedrive rclone",
        ["Network"] = "retea wifi dns",
        ["ResolveTools"] = "davinci resolve randare email",
        ["ProcessMonitor"] = "procese ram inchide",
    };

    /// Filtreaza randurile din ModuleList dupa nume+tooltip+sinonime -
    /// diacritice ignorate (userul scrie des fara diacritice).
    private void OnSearchTextChanged(object sender, TextChangedEventArgs e)
    {
        var query = RemoveDiacritics(SearchBox.Text).ToLowerInvariant().Trim();
        foreach (var obj in ModuleList.Items)
        {
            if (obj is not ListBoxItem item) continue;
            if (string.IsNullOrEmpty(query)) { item.Visibility = Visibility.Visible; continue; }
            var tag = item.Tag as string ?? "";
            var haystack = RemoveDiacritics($"{item.Content} {item.ToolTip} {(SearchKeywords.TryGetValue(tag, out var kw) ? kw : "")}").ToLowerInvariant();
            item.Visibility = haystack.Contains(query) ? Visibility.Visible : Visibility.Collapsed;
        }
    }

    private static string RemoveDiacritics(string text)
    {
        var normalized = text.Normalize(System.Text.NormalizationForm.FormD);
        var chars = normalized.Where(c => System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c) != System.Globalization.UnicodeCategory.NonSpacingMark);
        return new string(chars.ToArray()).Normalize(System.Text.NormalizationForm.FormC);
    }

    private void OnModuleSelected(object sender, SelectionChangedEventArgs e)
    {
        if (ModuleList.SelectedItem is not ListBoxItem item) return;
        PageHost.Content = (item.Tag as string) switch
        {
            "RenderMode" => new Pages.RenderModePage(),
            "LoginItems" => new Pages.LoginItemsPage(),
            "ProcessMonitor" => new Pages.ProcessMonitorPage(),
            "Uninstaller" => new Pages.UninstallerPage(),
            "DiskHealth" => new Pages.DiskHealthPage(),
            "DiskAnalyzer" => new Pages.DiskAnalyzerPage(),
            "ResolveTools" => new Pages.ResolveToolsPage(),
            "WindowLayouts" => new Pages.WindowLayoutsPage(),
            "Network" => new NetworkPage(),
            "Cloud" => new Pages.CloudPage(),
            "Cleanup" => new Pages.CleanupPage(),
            "Duplicates" => new Pages.DuplicateFinderPage(),
            "Security" => new Pages.SecurityPage(),
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

    private void RefreshLicenseBadge()
    {
        var unlocked = LicenseManager.Shared.IsUnlocked;
        LicenseBadge.Content = unlocked ? "Pro" : "Trial — Activează";
        LicenseBadge.Foreground = unlocked ? System.Windows.Media.Brushes.LimeGreen : System.Windows.Media.Brushes.Orange;
    }

    private void OnLicenseBadgeClicked(object sender, RoutedEventArgs e)
    {
        if (LicenseManager.Shared.IsUnlocked) return;
        var gate = new TrialGateWindow { Owner = this };
        if (gate.ShowDialog() == true) RefreshLicenseBadge();
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
            Content = $"Master Control Studio Pro {version} este disponibil (tu ai {UpdateChecker.CurrentVersion}).",
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
    ///
    /// [2026-09-03] FIX REAL, raportat de Cristi: la "Mărime Text" mare,
    /// anumite pagini (ex. Curățare & RAM) "nu se încărcau total, se
    /// blocau, fără slider lateral" - cauza reala: `LayoutTransform` pe
    /// `RootGrid` mareste vizual TOT continutul, dar fereastra insasi
    /// (`Width="1000" Height="680"`, fixa) NU se marea odata cu el -
    /// continutul scalat (inclusiv bara de scroll din dreapta unui
    /// `ScrollViewer`) ajungea pur si simplu in afara marginii fizice a
    /// ferestrei, invizibil si inaccesibil, desi ScrollViewer-ul insusi
    /// functiona corect intern. Fix: fereastra se redimensioneaza acum
    /// odata cu scala (limitata la ecranul disponibil), ca intreg
    /// continutul scalat sa ramana efectiv vizibil.
    private static readonly double BaseWidth = 1000;
    private static readonly double BaseHeight = 680;

    public void ApplyTextScale(TextScalePreference preference)
    {
        var scale = preference.ScaleFactor();
        RootGrid.LayoutTransform = scale == 1.0 ? Transform.Identity : new ScaleTransform(scale, scale);

        var workArea = SystemParameters.WorkArea;
        Width = Math.Min(BaseWidth * scale, workArea.Width);
        Height = Math.Min(BaseHeight * scale, workArea.Height);
        MinWidth = Math.Min(820 * scale, workArea.Width);
        MinHeight = Math.Min(520 * scale, workArea.Height);
    }
}
