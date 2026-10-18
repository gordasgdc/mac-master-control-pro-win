using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using MacMasterControlPro.Core.Services;
using Wpf.Ui.Appearance;

namespace MacMasterControlPro.Client;

public partial class App : Application
{
    /// Accent amber/cupru (Regula 7/16) — face toate controalele Wpf.Ui
    /// sa foloseasca aceeasi paleta ca Mac/gordas.dev, fara stiluri manuale.
    ///
    /// BUG REAL, gasit 2026-08-30 dupa primul test pe Windows real (Cristi):
    /// `App.xaml` nu are `StartupUri` si nicaieri in cod nu se apela
    /// `new MainWindow().Show()` — aplicatia pornea, aplica tema, si se
    /// oprea acolo. Procesul ramanea "viu" in Task Manager (WPF nu iese
    /// singur cat timp n-a fost deschisa NICIO fereastra vreodata — vezi
    /// `ShutdownMode.OnLastWindowClose`), dar nicio fereastra nu aparea
    /// vreodata pe ecran. `dotnet build` nu prinde asta niciodata (XAML/BAML
    /// compileaza identic cu sau fara StartupUri) — doar rularea reala pe
    /// Windows a aratat problema. Fix: deschidem fereastra explicit aici,
    /// intr-un try/catch care scrie in DiagnosticLog + arata un MessageBox
    /// vizibil daca pornirea crapa - inainte, un crash la pornire ar fi fost
    /// la fel de invizibil ca lipsa ferestrei (WPF fara consola ataseta nu
    /// arata nimic pe ecran la o exceptie nehandled inainte de shell-ul de
    /// exceptii default, care poate fi suprimat de politici Windows/AV).
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // BUG REAL, gasit 2026-08-30 (raport Cristi): "Adauga cont Cloud"
        // (rclone) esua cu "the system cannot find the file specified" si
        // WorkingDirectory = "C:\Program Files\GDC Plugin Manager" - userul
        // lansase Master Control Studio Pro din butonul "Deschide" al GDC
        // Plugin Manager. `Process.Start(UseShellExecute:true)` FARA
        // WorkingDirectory explicit mosteneste directorul curent al
        // PARINTELUI (GDC Plugin Manager), nu directorul propriu al acestei
        // aplicatii - orice comanda ulterioara pornita de-aici cu o cale
        // relativa/WorkingDirectory implicit ar fi cautat-o gresit acolo.
        // Resetam explicit la propriul folder, indiferent cine ne-a lansat.
        Environment.CurrentDirectory = AppContext.BaseDirectory;

        DiagnosticLog.Write("App", "OnStartup: pornire aplicatie.");
        PrivilegedPlan.Approver = (labels, title) =>
        {
            // Verificare automata (profil izolat cu MCSP_DEMO_ELEVATION): planul se aproba fara fereastra; doar promptul UAC e inlocuit de stub.
            if (MacMasterControlPro.Core.Services.AppProfile.IsIsolated && (Environment.GetEnvironmentVariable("MCSP_DEMO_ELEVATION") is "ok" or "cancel")) return true;
            return Dispatcher.Invoke(() =>
            {
                var shown = labels.Take(10).Select(l => "• " + (l.Contains('#') ? l[..l.LastIndexOf('#')] : l));
                var more = labels.Count > 10 ? "\n" + string.Format(L.X("… și încă {0}", "… and {0} more", "… y {0} más"), labels.Count - 10) : "";
                var text = string.Format(L.X("{0} elemente cer drepturi de administrator:", "{0} items need administrator rights:", "{0} elementos requieren derechos de administrador:"), labels.Count) + "\n\n" + string.Join("\n", shown) + more + "\n\n" +
                    L.X("Windows va cere confirmarea UAC o SINGURĂ dată pentru acest plan. Nimic nu rămâne autorizat după el. Dacă anulezi, nu se modifică nimic.", "Windows will ask for UAC confirmation ONCE for this plan. Nothing stays authorised afterwards. If you cancel, nothing is changed.", "Windows pedirá la confirmación de UAC UNA sola vez para este plan. Nada queda autorizado después. Si cancelas, no se modifica nada.");
                return System.Windows.MessageBox.Show(MainWindow, text, title, MessageBoxButton.OKCancel, MessageBoxImage.Warning) == MessageBoxResult.OK;
            });
        };

        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
            DiagnosticLog.Write("App", $"UnhandledException (fatal): {DiagnosticLog.Describe(args.ExceptionObject as Exception ?? new Exception("necunoscuta"))}");
        DispatcherUnhandledException += (_, args) =>
        {
            DiagnosticLog.Write("App", $"DispatcherUnhandledException: {DiagnosticLog.Describe(args.Exception)}");
            MessageBox.Show(
                $"A aparut o eroare neasteptata:\n\n{args.Exception.Message}\n\nDetalii complete in: {DiagnosticLog.FilePath}",
                "Master Control Studio Pro", MessageBoxButton.OK, MessageBoxImage.Error);
            args.Handled = true;
        };

        try
        {
            DiagnosticLog.Write("App", "Aplic tema...");
            Services.ThemeManager.Apply();
            ApplicationAccentColorManager.Apply(Color.FromRgb(0xD9, 0x8A, 0x3D));

            DiagnosticLog.Write("App", "Creez si afisez MainWindow...");
            var window = new MainWindow();
            MainWindow = window;
            window.Show();
            DiagnosticLog.Write("App", "MainWindow.Show() a reusit.");
        }
        catch (Exception ex)
        {
            DiagnosticLog.Write("App", $"CRASH la pornire: {DiagnosticLog.Describe(ex)}");
            MessageBox.Show(
                $"Aplicatia nu a putut porni:\n\n{ex.Message}\n\nDetalii complete in: {DiagnosticLog.FilePath}\n\nTrimite acest fisier pentru diagnosticare.",
                L.X("Master Control Studio Pro — Eroare la pornire", "Master Control Studio Pro — Startup error", "Master Control Studio Pro — Error al iniciar"), MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown(1);
        }
    }
}
