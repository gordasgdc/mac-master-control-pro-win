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
                "Master Control Studio Pro — Eroare la pornire", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown(1);
        }
    }
}
