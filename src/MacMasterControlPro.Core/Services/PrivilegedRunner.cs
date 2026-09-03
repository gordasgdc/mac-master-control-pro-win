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
    public static bool Run(string command, Action<string>? onOutput = null)
    {
        var logPath = Path.Combine(Path.GetTempPath(), $"mmcp-elevated-{Guid.NewGuid():N}.log");
        // try/catch prinde erorile TERMINATOARE PowerShell (ex. cmdlet-uri
        // native gresit scrise) - comenzile externe (sc.exe) care esueaza
        // cu cod de iesire nenul NU arunca aici, dar mesajul lor de eroare
        // tot ajunge in log prin `*>` (redirecteaza si stdout si stderr).
        var wrapped = $"try {{ {command} }} catch {{ Write-Output \"MMCP_ERROR: $($_.Exception.Message)\" }}; Write-Output 'MMCP_DONE'";
        var escaped = wrapped.Replace("\"", "\\\"");
        var psi = new ProcessStartInfo
        {
            FileName = "powershell.exe",
            Arguments = $"-NoProfile -Command \"{escaped} *> '{logPath}'\"",
            UseShellExecute = true,
            Verb = "runas", // declanseaza promptul UAC nativ
            CreateNoWindow = true,
            WindowStyle = ProcessWindowStyle.Hidden,
        };
        try
        {
            using var process = Process.Start(psi);
            process?.WaitForExit();
        }
        catch (System.ComponentModel.Win32Exception)
        {
            // Userul a respins promptul UAC, sau elevarea a esuat la nivel
            // de sistem - fara log de citit in acest caz.
            onOutput?.Invoke("✘ Promptul UAC a fost respins.");
            return false;
        }

        string[] lines;
        try { lines = File.Exists(logPath) ? File.ReadAllLines(logPath) : Array.Empty<string>(); }
        catch { lines = Array.Empty<string>(); }
        finally { try { if (File.Exists(logPath)) File.Delete(logPath); } catch { /* ignora */ } }

        foreach (var line in lines)
        {
            var trimmed = line.Trim();
            if (trimmed.Length == 0 || trimmed == "MMCP_DONE") continue;
            onOutput?.Invoke(trimmed);
        }

        // "Completat" = scriptul a rulat pana la capat (marker gasit),
        // indiferent daca vreo comanda externa individuala a raportat un
        // cod de iesire nenul benign (ex. "sc stop" pe un serviciu deja
        // oprit) - acele mesaje sunt deja vizibile mai sus, in log.
        return lines.Any(l => l.Trim() == "MMCP_DONE");
    }

    public static bool Run(IEnumerable<string> commands, Action<string>? onOutput = null) =>
        Run(string.Join("; ", commands), onOutput);
}
