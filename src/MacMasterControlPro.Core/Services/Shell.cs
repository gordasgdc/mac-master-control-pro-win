using System.Diagnostics;

namespace MacMasterControlPro.Core.Services;

/// Executie shell fara privilegii (netsh read-only, scanari, brew-equivalent).
public static class Shell
{
    /// [2026-09-03] FIX REAL, port 1:1 al fix-ului gasit pe Mac
    /// (Shell.swift): `StandardOutput.ReadToEnd()` era apelat cu
    /// `RedirectStandardError = true`, dar `StandardError` NU era CITIT
    /// NICIODATA. Deadlock clasic .NET, documentat chiar de Microsoft
    /// (`Process` remarks): bufferul de pipe al `stderr` are o dimensiune
    /// FIXA - daca procesul copil scrie destul pe stderr (ex. un `Get-ChildItem`
    /// recursiv pe un disc extern mare, cu multe erori "Access Denied" pe
    /// foldere protejate), bufferul se umple, copilul se blocheaza la
    /// scriere, si NICIODATA nu mai apuca sa inchida stdout - deci
    /// `ReadToEnd()` pe stdout ramane blocat la nesfarsit si el, chiar daca
    /// stdout in sine era mic. Exact tiparul "aplicatia ingheata la
    /// scanare" gasit si reparat pe partea de Mac.
    /// Fix: citim AMBELE fluxuri ASINCRON (`OutputDataReceived`/
    /// `ErrorDataReceived` + `BeginOutputReadLine`/`BeginErrorReadLine`),
    /// niciodata sincron dupa `WaitForExit()` - parintele goleste mereu
    /// ambele buffere pe masura ce sosesc date, copilul nu se mai poate
    /// bloca la scriere indiferent cat de mare e output-ul.
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
            using var process = new Process { StartInfo = psi, EnableRaisingEvents = true };
            var output = new System.Text.StringBuilder();
            var sync = new object();
            process.OutputDataReceived += (_, e) => { if (e.Data is not null) lock (sync) output.AppendLine(e.Data); };
            process.ErrorDataReceived += (_, e) => { if (e.Data is not null) lock (sync) output.AppendLine(e.Data); };
            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
            process.WaitForExit();
            lock (sync) return output.ToString().Trim();
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
