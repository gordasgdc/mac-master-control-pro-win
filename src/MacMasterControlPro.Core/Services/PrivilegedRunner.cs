using System.Diagnostics;

namespace MacMasterControlPro.Core.Services;

/// Executie privilegiata (echivalent PrivilegedRunner.swift / AppleScript
/// admin) — porneste PowerShell elevat prin UAC nativ (`Verb = "runas"`),
/// singurul prompt de sistem, fara sudo/Terminal vizibil.
///
/// [2026-09-03] FIX REAL, raportat de Cristi: un esec la "Mod Randare"/
/// "Pornire Sistem" arata DOAR "eroare — promptul UAC a fost respins sau
/// comanda a eșuat", fara nicio indicatie CARE comanda si DE CE — panoul
/// „Terminal Live" (Regula 26) nu primea nimic real, pentru ca
/// `UseShellExecute = true` + `Verb = "runas"` (necesar pentru promptul UAC
/// nativ) face IMPOSIBILA redirectarea directa a stdout/stderr a
/// procesului elevat catre procesul parinte — o limitare reala a Windows,
/// nu un bug de cod ratat. Fix: procesul elevat isi scrie singur output-ul
/// (`*> logPath`) intr-un fisier temporar, pe care parintele il citeste
/// dupa `WaitForExit()` si il retransmite linie-cu-linie catre panoul
/// „Terminal Live" - acelasi rezultat vizibil ca o redirectare directa,
/// ocolind limitarea. Un marker `MMCP_DONE` scris la finalul scriptului
/// distinge "scriptul a rulat complet" de "s-a intrerupt la mijloc" -
/// codul de iesire al `powershell.exe` NU e de incredere singur aici (ramane
/// adesea 0 chiar daca o comanda externa din interiorul lantului `;` a
/// esuat, atata timp cat ultima instructiune din script nu arunca ea insasi
/// o eroare terminatoare).
public static class PrivilegedRunner
{
    /// `onOutput`, daca dat, primeste FIECARE linie reala scrisa de comanda
    /// elevata (inclusiv mesaje de eroare native, ex. "sc" raportand ca un
    /// serviciu era deja oprit) - cablat direct in panoul „Terminal Live"
    /// de apelanti (vezi RenderModeService/LoginItemsService).
    public sealed record RunResult(bool Completed, bool Cancelled, IReadOnlyList<string> Lines);

    /// Numărul de solicitări de autorizare (prompturi UAC) lansate de această sesiune. Folosit la verificare și în jurnal.
    public static int PromptCount => _promptCount;
    private static int _promptCount;

    /// Verificare automată (DOAR profil izolat, `MCSP_DEMO_ELEVATION=ok|cancel`): fără UAC real; numără solicitările și, pentru `ok`, rulează comanda cu drepturile utilizatorului pe fixture-uri.
    private static string? StubMode
    {
        get
        {
            if (!AppProfile.IsIsolated) return null;
            var v = Environment.GetEnvironmentVariable("MCSP_DEMO_ELEVATION");
            return v is "ok" or "cancel" ? v : null;
        }
    }

    public static bool Run(string command, Action<string>? onOutput = null, string reason = "")
    {
        var r = RunDetailed(command, reason);
        foreach (var l in r.Lines) onOutput?.Invoke(l);
        if (r.Cancelled) onOutput?.Invoke("✘ Promptul UAC a fost respins.");
        return r.Completed;
    }

    public static RunResult RunDetailed(string command, string reason = "")
    {
        var n = System.Threading.Interlocked.Increment(ref _promptCount);
        DiagnosticLog.Log("info", "privilege", "prompt", $"#{n} {reason}");
        var logPath = Path.Combine(Path.GetTempPath(), $"mmcp-elevated-{Guid.NewGuid():N}.log");
        // try/catch prinde erorile TERMINATOARE PowerShell; comenzile externe care esueaza cu cod nenul nu arunca, dar mesajul lor ajunge in log prin `*>`.
        var wrapped = $"try {{ {command} }} catch {{ Write-Output \"MMCP_ERROR: $($_.Exception.Message)\" }}; Write-Output 'MMCP_DONE'";
        var escaped = wrapped.Replace("\"", "\\\"");
        var stub = StubMode;
        if (stub is not null)
        {
            try { File.AppendAllText(Path.Combine(AppProfile.Local("MacMasterControlPro"), "elevation-prompts.log"), $"prompt #{n} {stub} {reason}{Environment.NewLine}"); } catch { }
            if (stub == "cancel") return new RunResult(false, true, Array.Empty<string>());
        }
        var psi = new ProcessStartInfo
        {
            FileName = "powershell.exe",
            Arguments = $"-NoProfile -Command \"{escaped} *> '{logPath}'\"",
            UseShellExecute = true,
            CreateNoWindow = true,
            WindowStyle = ProcessWindowStyle.Hidden,
        };
        if (stub is null) psi.Verb = "runas";   // declanseaza promptul UAC nativ
        try
        {
            using var process = Process.Start(psi);
            process?.WaitForExit();
        }
        catch (System.ComponentModel.Win32Exception e)
        {
            // 1223 = ERROR_CANCELLED: utilizatorul a refuzat promptul UAC. Planul se opreste, nu se mai cere nimic.
            return new RunResult(false, e.NativeErrorCode == 1223, Array.Empty<string>());
        }

        string[] lines;
        try { lines = File.Exists(logPath) ? File.ReadAllLines(logPath) : Array.Empty<string>(); }
        catch { lines = Array.Empty<string>(); }
        finally { try { if (File.Exists(logPath)) File.Delete(logPath); } catch { /* ignora */ } }

        var trimmed = lines.Select(l => l.Trim()).Where(l => l.Length > 0 && l != "MMCP_DONE").ToList();
        return new RunResult(lines.Any(l => l.Trim() == "MMCP_DONE"), false, trimmed);
    }

    public static bool Run(IEnumerable<string> commands, Action<string>? onOutput = null) =>
        Run(string.Join("; ", commands), onOutput);
}
