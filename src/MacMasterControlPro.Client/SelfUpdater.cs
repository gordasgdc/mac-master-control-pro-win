using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using System.Windows;
using MacMasterControlPro.Core.Services;

namespace MacMasterControlPro.Client;

/// Descarca si lanseaza automat installer-ul de update, fara browser
/// (Regula 20) — port 1:1 al SelfUpdater.cs din GDCVaultWin.
///
/// WARNING: pasul de instalare efectiv (wizard-ul Inno) NU poate fi
/// verificat automat.
public static class SelfUpdater
{
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromMinutes(5) };

    public static async Task DownloadAndInstallAsync(string version)
    {
        var progress = new UpdateProgressWindow(version);
        progress.Show();

        try
        {
            var tempDir = Path.Combine(Path.GetTempPath(), "mmc-update-" + Guid.NewGuid());
            Directory.CreateDirectory(tempDir);

            progress.SetStatus("Se descarcă actualizarea…");
            var exePath = Path.Combine(tempDir, $"MacMasterControlProSetup-{version}.exe");
            await DownloadAsync(UpdateChecker.DirectDownloadUrl.ToString(), exePath);

            progress.SetStatus("Se lansează instalatorul…");
            Process.Start(new ProcessStartInfo(exePath) { UseShellExecute = true });

            progress.Close();
            Application.Current.Shutdown();
        }
        catch (Exception ex)
        {
            progress.Close();
            PresentFailure(ex.Message);
        }
    }

    private static async Task DownloadAsync(string url, string destination)
    {
        using var response = await Http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"Descărcarea a eșuat: HTTP {(int)response.StatusCode}");
        }
        await using var httpStream = await response.Content.ReadAsStreamAsync();
        await using var fileStream = File.Create(destination);
        await httpStream.CopyToAsync(fileStream);
    }

    private static void PresentFailure(string message)
    {
        var box = new Wpf.Ui.Controls.MessageBox
        {
            Title = "Actualizarea a eșuat",
            Content = $"{message}\n\nPoți descărca manual ultima versiune de pe pagina de GitHub.",
            PrimaryButtonText = "Deschide pagina",
            CloseButtonText = "OK",
        };
        _ = ShowFailureAsync(box);
    }

    private static async Task ShowFailureAsync(Wpf.Ui.Controls.MessageBox box)
    {
        var result = await box.ShowDialogAsync();
        if (result == Wpf.Ui.Controls.MessageBoxResult.Primary)
        {
            Process.Start(new ProcessStartInfo(UpdateChecker.ReleasesPageUrl.ToString()) { UseShellExecute = true });
        }
    }
}
