using System.Diagnostics;

namespace MacMasterControlPro.Core.Services;

/// Executie shell fara privilegii (netsh read-only, scanari, brew-equivalent).
public static class Shell
{
    public static string Run(string command)
    {
        var psi = new ProcessStartInfo
        {
            FileName = "powershell.exe",
            Arguments = $"-NoProfile -NonInteractive -Command \"{command.Replace("\"", "\\\"")}\"",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        // BUG REAL, gasit 2026-08-30 (raport Cristi): dupa un `winget install`
        // reusit, comanda de re-verificare (ex. `rclone version`) tot raporta
        // "Neinstalat" - procesul GUI e de lunga durata, deci PATH-ul mostenit
        // de orice sub-proces pornit de el ramane cel de la LANSAREA aplicatiei;
        // winget actualizeaza PATH-ul doar in Registry, nu si in procesele deja
        // pornite. Fortam aici PATH-ul proaspat (Machine+User), citit direct din
        // Registry la fiecare apel - un CLI nou instalat e vazut instant, fara
        // sa fie nevoie de o repornire a aplicatiei.
        psi.EnvironmentVariables["Path"] = RefreshedPath();
        try
        {
            using var process = Process.Start(psi);
            if (process is null) return string.Empty;
            var output = process.StandardOutput.ReadToEnd();
            process.WaitForExit();
            return output.Trim();
        }
        catch (Exception ex)
        {
            return $"Eroare executie: {ex.Message}";
        }
    }

    private static string RefreshedPath()
    {
        var machine = Environment.GetEnvironmentVariable("Path", EnvironmentVariableTarget.Machine) ?? "";
        var user = Environment.GetEnvironmentVariable("Path", EnvironmentVariableTarget.User) ?? "";
        return string.IsNullOrEmpty(user) ? machine : $"{machine};{user}";
    }
}
