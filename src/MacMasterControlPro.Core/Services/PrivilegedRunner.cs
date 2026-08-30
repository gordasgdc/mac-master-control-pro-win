using System.Diagnostics;

namespace MacMasterControlPro.Core.Services;

/// Executie privilegiata (echivalent PrivilegedRunner.swift / AppleScript
/// admin) — porneste PowerShell elevat prin UAC nativ (`Verb = "runas"`),
/// singurul prompt de sistem, fara sudo/Terminal vizibil.
public static class PrivilegedRunner
{
    public static bool Run(string command)
    {
        var psi = new ProcessStartInfo
        {
            FileName = "powershell.exe",
            Arguments = $"-NoProfile -Command \"{command.Replace("\"", "\\\"")}\"",
            UseShellExecute = true,
            Verb = "runas", // declanseaza promptul UAC nativ
            CreateNoWindow = false,
        };
        try
        {
            using var process = Process.Start(psi);
            process?.WaitForExit();
            return process?.ExitCode == 0;
        }
        catch (System.ComponentModel.Win32Exception)
        {
            // Userul a respins promptul UAC.
            return false;
        }
    }

    public static bool Run(IEnumerable<string> commands) => Run(string.Join("; ", commands));
}
