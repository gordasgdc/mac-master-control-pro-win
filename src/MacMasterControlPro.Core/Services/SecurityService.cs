namespace MacMasterControlPro.Core.Services;

/// Oglinda SecurityService.swift (Mac) — verificari 🔴/🟢 + 2 actiuni
/// sigure, fara risc de blocare a sistemului sau BitLocker fara cheie de
/// recuperare confirmata manual.
public sealed record SecurityCheck(string Id, string Title, bool IsGood, string Detail);

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
        return new SecurityCheck("bitlocker", "BitLocker (criptare disc)", on,
            on ? "Activ" : "Dezactivat — activează-l manual din Control Panel (cere cheie de recuperare, nu se automatizează)");
    }

    private static SecurityCheck DefenderCheck()
    {
        var output = Shell.Run("(Get-MpComputerStatus).RealTimeProtectionEnabled");
        var on = output.Trim().Equals("True", StringComparison.OrdinalIgnoreCase);
        return new SecurityCheck("defender", "Windows Defender (protecție în timp real)", on, on ? "Activ" : "Dezactivat");
    }

    private static SecurityCheck FirewallCheck()
    {
        var output = Shell.Run("(Get-NetFirewallProfile -Profile Domain,Public,Private).Enabled -join ','");
        var on = output.Contains("True");
        return new SecurityCheck("firewall", "Firewall Windows", on, on ? "Activ" : "Dezactivat pe cel puțin un profil");
    }

    private static SecurityCheck SecureBootCheck()
    {
        var output = Shell.Run("Confirm-SecureBootUEFI");
        var on = output.Trim().Equals("True", StringComparison.OrdinalIgnoreCase);
        return new SecurityCheck("secureboot", "Secure Boot", on, on ? "Activ" : "Dezactivat sau nesuportat (BIOS legacy)");
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
