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
        LicenseManager.Shared.Changed += () => Dispatcher.Invoke(RefreshLicenseBadge);
        MacMasterControlPro.Client.Services.AccessCoordinator.Start();
        MachineIdButton.Content = $"Machine ID: {MachineID.Display[..Math.Min(13, MachineID.Display.Length)]}…";

        _dependencyChecker.CheckAll();
        RefreshDependencyBadge();

        ArrangeSidebarGroups();
        RenderSidebarLabels();
        LanguageStore.Changed += RenderSidebarLabels;
        LanguageStore.Changed += () => { if (PageHost.Content is DashboardPage) PageHost.Content = new DashboardPage(); };

        PageHost.Content = new DashboardPage();
        // Evidentiaza vizual "Dashboard" in lista - facut aici, NU cu
        // IsSelected="True" in XAML (vezi comentariul din MainWindow.xaml):
        // la acest punct PageHost e deja asignat, deci OnModuleSelected
        // (declansat de asta) e sigur.
        ModuleList.SelectedItem = ItemDashboard;
        if (DemoHooks.Page is { } demoPage) NavigateTo(demoPage);

        _ = MaybeShowUpdatePopupAsync(respectDismissal: true);
    }

    private static readonly System.Windows.Media.FontFamily IconFont = new("Segoe Fluent Icons, Segoe MDL2 Assets");

    /// Eticheta din bara laterală: pictogramă desenată (nu emoji) + denumire clară.
    private static void SetLabel(ListBoxItem item, string glyph, string text)
    {
        var sp = new StackPanel { Orientation = Orientation.Horizontal };
        sp.Children.Add(new TextBlock { Text = glyph, FontFamily = IconFont, FontSize = 16, Width = 24, VerticalAlignment = VerticalAlignment.Center });
        sp.Children.Add(new TextBlock { Text = text, VerticalAlignment = VerticalAlignment.Center });
        item.Content = sp;
        System.Windows.Automation.AutomationProperties.SetName(item, text);
    }
    private static string LabelOf(ListBoxItem item) => item.Content is StackPanel sp && sp.Children.Count > 1 && sp.Children[1] is TextBlock t ? t.Text : item.Content?.ToString() ?? "";

    /// Toate modulele rămân, grupate după ce vrea utilizatorul să facă.
    private void ArrangeSidebarGroups()
    {
        var groups = new (string Title, ListBoxItem[] Items)[]
        {
            (L.X("Acasă","Home","Inicio"), new[] { ItemDashboard }),
            (L.X("Spațiu","Space","Espacio"), new[] { ItemDiskAnalyzer, ItemCleanup, ItemDuplicates, ItemUninstaller }),
            (L.X("Sistem și activitate","System & activity","Sistema y actividad"), new[] { ItemProcessMonitor, ItemLoginItems, ItemDiskHealth, ItemRenderMode, ItemNetwork, ItemSecurity, ItemTweaks, ItemWindowLayouts }),
            (L.X("Media și Resolve","Media & Resolve","Medios y Resolve"), new[] { ItemResolveTools, ItemCloud }),
            (L.X("Aplicație","App","Aplicación"), new[] { DependenciesItem, ItemSettings }),
        };
        ModuleList.Items.Clear();
        foreach (var (title, items) in groups)
        {
            ModuleList.Items.Add(new ListBoxItem { Content = title, IsEnabled = false, Focusable = false, IsHitTestVisible = false, FontSize = 11, FontWeight = FontWeights.SemiBold, Opacity = 0.75, Margin = new Thickness(4, 10, 0, 2), Tag = "__header" });
            foreach (var i in items) ModuleList.Items.Add(i);
        }
    }

    /// Navigare programatică (ex. din Acasă către Analiză Disc).
    public void NavigateTo(string tag)
    {
        foreach (var o in ModuleList.Items)
            if (o is ListBoxItem i && (i.Tag as string) == tag) { ModuleList.SelectedItem = i; return; }
    }

    private void RenderSidebarLabels()
    {
        SetLabel(ItemDashboard, "\uE80F", L.T("sidebar.dashboard"));
        SetLabel(ItemRenderMode, "\uE945", L.T("sidebar.renderMode"));
        SetLabel(ItemLoginItems, "\uE7E8", L.T("sidebar.loginItems"));
        SetLabel(ItemProcessMonitor, "\uE950", L.T("sidebar.processMonitor"));
        SetLabel(ItemUninstaller, "\uE71D", L.T("sidebar.uninstaller"));
        SetLabel(ItemDiskHealth, "\uEDA2", L.T("sidebar.diskHealth"));
        SetLabel(ItemDiskAnalyzer, "\uEB05", L.T("sidebar.diskAnalyzer"));
        SetLabel(ItemResolveTools, "\uE714", L.T("sidebar.resolveTools"));
        SetLabel(ItemWindowLayouts, "\uE8A1", L.T("sidebar.windowLayouts"));
        SetLabel(ItemNetwork, "\uE774", L.T("sidebar.network"));
        SetLabel(ItemCloud, "\uE753", L.T("sidebar.cloud"));
        SetLabel(ItemCleanup, "\uE74D", L.T("sidebar.cleanup"));
        SetLabel(ItemDuplicates, "\uE8C8", L.T("sidebar.duplicates"));
        SetLabel(ItemSecurity, "\uEA18", L.T("sidebar.security"));
        SetLabel(ItemTweaks, "\uE9E9", L.T("sidebar.tweaks"));
        SetLabel(DependenciesItem, "\uE7B8", L.T("sidebar.dependencies"));
        SetLabel(ItemSettings, "\uE713", L.T("sidebar.settings"));
        CheckUpdatesButton.Content = L.T("sidebar.checkUpdates");
        SearchBox.PlaceholderText = L.X("Caută (duplicate, dezinstalare, reglare)…", "Search (duplicates, uninstall, tweaks)…", "Buscar (duplicados, desinstalar, ajustes)…");

        // Cerinta directa (2026-09-01): "cand te duci cu mouse-ul peste un
        // buton, sa-ti apara o descriere de ce face".
        ItemDashboard.ToolTip = L.X("Privire de ansamblu — starea generală a PC-ului dintr-o privire.", "Overview — the general state of your PC at a glance.", "Resumen: el estado general del equipo de un vistazo.");
        ItemRenderMode.ToolTip = L.X("Oprește temporar Windows Search/File History cât randezi în DaVinci Resolve.", "Temporarily pauses Windows Search/File History while you render in DaVinci Resolve.", "Pausa temporalmente Windows Search/Historial de archivos mientras renderizas en DaVinci Resolve.");
        ItemLoginItems.ToolTip = L.X("Vezi și oprești aplicațiile care pornesc automat odată cu Windows.", "See and stop apps that start automatically with Windows.", "Consulta y detén las apps que se inician con Windows.");
        ItemProcessMonitor.ToolTip = L.X("Procesele active acum, sortabile după RAM — închide ce consumă prea mult.", "Running processes, sortable by RAM — close what uses too much.", "Procesos activos, ordenables por RAM: cierra lo que consume demasiado.");
        ItemUninstaller.ToolTip = L.X("Dezinstalează complet una sau mai multe aplicații, cu toate urmele lor.", "Fully uninstall one or more apps, leftovers included.", "Desinstala por completo una o varias apps, con todos sus restos.");
        ItemDiskHealth.ToolTip = L.X("Spațiu liber, status SMART și test de viteză pentru discurile montate.", "Free space, SMART status and speed test for mounted drives.", "Espacio libre, estado SMART y prueba de velocidad de las unidades.");
        ItemDiskAnalyzer.ToolTip = L.X("Vezi ce ocupă spațiul pe disc, folder cu folder — indexare o singură dată, apoi navigare instantă.", "See what uses disk space, folder by folder — index once, then browse instantly.", "Mira qué ocupa espacio en el disco, carpeta por carpeta: se indexa una vez y luego navegas al instante.");
        ItemResolveTools.ToolTip = L.X("Notificare la final de randare, verificare Media Pool, sincronizare LUT-uri, backup bază de date.", "Render-finished notification, Media Pool check, LUT sync, database backup.", "Aviso al terminar el render, revisión del Media Pool, sincronización de LUT y copia de la base de datos.");
        ItemWindowLayouts.ToolTip = L.X("Salvează și restaurează aranjamentul ferestrelor pe ecran.", "Save and restore your window arrangement.", "Guarda y restaura la disposición de las ventanas.");
        ItemNetwork.ToolTip = L.X("Configurare și optimizare rețea, persistentă la repornire.", "Network setup and tuning that persists across restarts.", "Configuración y optimización de red que persiste tras reiniciar.");
        ItemCloud.ToolTip = L.X("Conectează și gestionează conturi Cloud (Drive, Dropbox, S3 și altele).", "Connect and manage cloud accounts (Drive, Dropbox, S3 and more).", "Conecta y gestiona cuentas en la nube (Drive, Dropbox, S3 y más).");
        ItemCleanup.ToolTip = L.X("Șterge cache-uri recuperabile, fișiere mari uitate, eliberează RAM.", "Clear recoverable caches and forgotten large files, free RAM.", "Elimina cachés recuperables y archivos grandes olvidados, libera RAM.");
        ItemDuplicates.ToolTip = L.X("Găsește fișiere identice ca și conținut și te lasă să alegi ce ștergi.", "Find files with identical content and choose what to remove.", "Encuentra archivos con contenido idéntico y elige qué eliminar.");
        ItemSecurity.ToolTip = L.X("Verifică setările de securitate ale PC-ului, cu ghid pas-cu-pas pentru ce lipsește.", "Check your PC's security settings, with step-by-step guidance for what is missing.", "Revisa los ajustes de seguridad del equipo, con una guía paso a paso de lo que falta.");
        ItemTweaks.ToolTip = L.X("Ajustări rapide de sistem.", "Quick system tweaks.", "Ajustes rápidos del sistema.");
        DependenciesItem.ToolTip = L.X("Componentele externe de care aplicația are nevoie — instalare cu un click.", "External components the app needs — one-click install.", "Componentes externos que necesita la app: instalación con un clic.");
        ItemSettings.ToolTip = L.X("Temă, limbă, licență și alte preferințe ale aplicației.", "Theme, language, licence and other app preferences.", "Tema, idioma, licencia y otras preferencias de la app.");
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
            if (tag == "__header") { item.Visibility = Visibility.Collapsed; continue; }
            var haystack = RemoveDiacritics($"{LabelOf(item)} {item.ToolTip} {(SearchKeywords.TryGetValue(tag, out var kw) ? kw : "")}").ToLowerInvariant();
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
        ProfileNameText.Text = string.IsNullOrWhiteSpace(name) ? L.X("Anonim", "Anonymous", "Anónimo") : name;
        ProfileEmailText.Text = UserProfileStore.Email;
    }

    private void RefreshLicenseBadge()
    {
        var mode = LicenseManager.Shared.Mode;
        LicenseBadge.Content = mode switch
        {
            MacMasterControlPro.Core.Services.Access.AccessMode.Full => "Acces complet",
            MacMasterControlPro.Core.Services.Access.AccessMode.DemoAvailable d => $"{L.X("Probă", "Trial", "Prueba")} {d.RemainingSeconds / 60}:{d.RemainingSeconds % 60:00}",
            _ => L.X("Probă încheiată", "Trial ended", "Prueba finalizada"),
        };
        LicenseBadge.Foreground = mode.IsFull ? System.Windows.Media.Brushes.LimeGreen : mode.AllowsFeatures ? System.Windows.Media.Brushes.Orange : System.Windows.Media.Brushes.OrangeRed;
        LockedOverlay.Visibility = mode.AllowsFeatures ? Visibility.Collapsed : Visibility.Visible;
    }

    private void OnLicenseBadgeClicked(object sender, RoutedEventArgs e)
    {
        var gate = new AccessGateWindow { Owner = this };
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
                    Title = L.X("Ești la zi", "You are up to date", "Estás al día"),
                    Content = string.Format(L.X("Rulezi deja ultima versiune ({0}).", "You are already running the latest version ({0}).", "Ya usas la última versión ({0})."), UpdateChecker.CurrentVersion),
                }.ShowDialogAsync();
            }
            return;
        }
        if (respectDismissal && UpdateChecker.Shared.WasDismissed(version)) return;

        var box = new Wpf.Ui.Controls.MessageBox
        {
            Title = L.X("Este disponibilă o versiune nouă", "A new version is available", "Hay una versión nueva disponible"),
            Content = string.Format(L.X("Master Control Studio Pro {0} este disponibil (tu ai {1}).", "Master Control Studio Pro {0} is available (you have {1}).", "Master Control Studio Pro {0} está disponible (tienes {1})."), version, UpdateChecker.CurrentVersion),
            PrimaryButtonText = L.X("Actualizează acum", "Update now", "Actualizar ahora"),
            CloseButtonText = L.X("Mai târziu", "Later", "Más tarde"),
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
