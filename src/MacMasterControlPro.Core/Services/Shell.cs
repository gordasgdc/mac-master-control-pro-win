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
}
