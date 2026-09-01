namespace MacMasterControlPro.Core.Services;

/// Oglinda SecurityService.swift (Mac) — verificari 🔴/🟢 + 2 actiuni
/// sigure, fara risc de blocare a sistemului sau BitLocker fara cheie de
/// recuperare confirmata manual.
///
/// BUG REAL/cerinta (oglinda fix-ului Mac, 2026-08-31: "doar imi arata
/// rosu/verde, nu ma ajuta cu nimic sa rezolv") — `ManualSteps`/`SettingsUri`
/// adaugate ca fiecare verificare care nu se rezolva automat sa arate
/// explicit CE sa faca userul, plus un link direct catre panoul Windows
/// Settings corect (`ms-settings:...`).
public sealed record SecurityCheck(string Id, string Title, bool IsGood, string Detail, IReadOnlyList<string> ManualSteps, string? SettingsUri);

public static class SecurityService
{
    public static List<SecurityCheck> RunAllChecks() => new()
    {
        BitLockerCheck(),
        DefenderCheck(),
        FirewallCheck(),
        SecureBootCheck(),
    };

    private static SecurityCheck BitLockerCheck()
    {
        var output = Shell.Run("(Get-BitLockerVolume -MountPoint $env:SystemDrive).ProtectionStatus");
        var on = output.Trim() == "1" || output.Contains("On", StringComparison.OrdinalIgnoreCase);
        return new SecurityCheck("bitlocker", "BitLocker (criptare disc)", on, on ? "Activ" : "Dezactivat",
            on ? Array.Empty<string>() : new[]
            {
                "Apasă „Deschide Settings” mai jos — se deschide direct panoul BitLocker.",
                "Apasă „Turn on BitLocker” lângă discul de sistem.",
                "Alege cum salvezi cheia de recuperare (cont Microsoft, fișier, sau tipărită) — NOTEAZ-O undeva sigur. Fără ea, dacă uiți parola, datele devin irecuperabile.",
                "Alege „Encrypt entire drive” și confirmă — criptarea continuă în fundal, poți folosi PC-ul normal cât timp se face.",
            },
            "ms-settings:deviceencryption");
    }

    private static SecurityCheck DefenderCheck()
    {
        var output = Shell.Run("(Get-MpComputerStatus).RealTimeProtectionEnabled");
        var on = output.Trim().Equals("True", StringComparison.OrdinalIgnoreCase);
        return new SecurityCheck("defender", "Windows Defender (protecție în timp real)", on, on ? "Activ" : "Dezactivat",
            on ? Array.Empty<string>() : new[]
            {
                "Apasă „Deschide Settings” mai jos.",
                "Mergi la „Virus & threat protection” → „Manage settings”.",
                "Activează switch-ul „Real-time protection”.",
            },
            "windowsdefender:");
    }

    private static SecurityCheck FirewallCheck()
    {
        var output = Shell.Run("(Get-NetFirewallProfile -Profile Domain,Public,Private).Enabled -join ','");
        var on = output.Contains("True");
        return new SecurityCheck("firewall", "Firewall Windows", on, on ? "Activ" : "Dezactivat pe cel puțin un profil",
            on ? Array.Empty<string>() : new[]
            {
                "Cel mai simplu: butonul „Activează Firewall pe toate profilele” din Acțiuni rapide, mai jos — un singur click.",
                "Manual: „Deschide Settings” → activează toate cele 3 switch-uri (Domain/Private/Public network).",
            },
            "ms-settings:windowsdefender");
    }

    private static SecurityCheck SecureBootCheck()
    {
        var output = Shell.Run("Confirm-SecureBootUEFI");
        var on = output.Trim().Equals("True", StringComparison.OrdinalIgnoreCase);
        return new SecurityCheck("secureboot", "Secure Boot", on, on ? "Activ" : "Dezactivat sau nesuportat (BIOS legacy)",
            on ? Array.Empty<string>() : new[]
            {
                "Nu se poate activa din Windows Settings — necesită BIOS/UEFI.",
                "Repornește PC-ul și apasă tasta de BIOS (de obicei Del, F2 sau F10 — apare pe ecran la pornire).",
                "Caută secțiunea „Boot” sau „Security” din BIOS și activează „Secure Boot”.",
                "Salvează și ieși (de obicei F10) — PC-ul repornește normal.",
            },
            null);
    }

    // MARK: - Actiuni (un singur prompt UAC pentru toate comenzile)

    public static bool EnableFirewallAllProfiles() =>
        PrivilegedRunner.Run("Set-NetFirewallProfile -Profile Domain,Public,Private -Enabled True");

    public static bool RequirePasswordImmediatelyOnWake() =>
        PrivilegedRunner.Run(new[]
        {
            "powercfg /change monitor-timeout-ac 5",
            "reg add \"HKCU\\Software\\Policies\\Microsoft\\Windows\\Control Panel\\Desktop\" /v ScreenSaverIsSecure /t REG_SZ /d 1 /f",
            "reg add \"HKCU\\Software\\Policies\\Microsoft\\Windows\\Control Panel\\Desktop\" /v ScreenSaverGracePeriod /t REG_SZ /d 0 /f",
        });
}
