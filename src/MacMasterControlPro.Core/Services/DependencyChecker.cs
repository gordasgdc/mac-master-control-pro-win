namespace MacMasterControlPro.Core.Services;

/// Echivalent Windows al DependencyChecker.swift (Mac) — winget in loc de
/// Homebrew, WinFSP in loc de macFUSE.
public sealed class DependencyItem
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public bool IsInstalled { get; set; }
    public string? Version { get; set; }
}

public sealed class DependencyChecker
{
    public List<DependencyItem> Items { get; private set; } = new();

    public bool AllInstalled => Items.Count > 0 && Items.All(i => i.IsInstalled);

    public void CheckAll()
    {
        var results = new List<DependencyItem>();

        // winget - vine implicit cu Windows 11 si Windows 10 recente (App Installer).
        var wingetVersion = Shell.Run("winget --version 2>$null");
        var wingetInstalled = !string.IsNullOrWhiteSpace(wingetVersion) && !wingetVersion.Contains("Eroare");
        results.Add(new DependencyItem { Id = "winget", Name = "Windows Package Manager", IsInstalled = wingetInstalled, Version = wingetInstalled ? wingetVersion : null });

        // Rclone - fie in PATH, fie instalat prin winget.
        var rcloneVersion = Shell.Run("rclone version 2>$null | Select-Object -First 1");
        var rcloneInstalled = !string.IsNullOrWhiteSpace(rcloneVersion) && rcloneVersion.Contains("rclone");
        results.Add(new DependencyItem { Id = "rclone", Name = "Rclone", IsInstalled = rcloneInstalled, Version = rcloneInstalled ? rcloneVersion : null });

        // WinFSP - verificat prin cheia de Uninstall din Registry (instalat ca MSI).
        var winfspInstalled = Shell.Run(
            "Get-ItemProperty HKLM:\\SOFTWARE\\WOW6432Node\\Microsoft\\Windows\\CurrentVersion\\Uninstall\\* -ErrorAction SilentlyContinue | Where-Object DisplayName -like 'WinFsp*' | Select-Object -First 1 -ExpandProperty DisplayName"
        );
        results.Add(new DependencyItem { Id = "winfsp", Name = "WinFSP", IsInstalled = !string.IsNullOrWhiteSpace(winfspInstalled), Version = winfspInstalled });

        Items = results;
    }

    /// Rclone/WinFSP - instalare non-interactiva prin winget (necesita winget prezent).
    public string InstallMissing()
    {
        var log = "";
        if (Items.FirstOrDefault(i => i.Id == "rclone")?.IsInstalled == false)
        {
            log += Shell.Run("winget install --id Rclone.Rclone -e --silent --accept-package-agreements --accept-source-agreements 2>&1") + "\n";
        }
        if (Items.FirstOrDefault(i => i.Id == "winfsp")?.IsInstalled == false)
        {
            log += Shell.Run("winget install --id WinFsp.WinFsp -e --silent --accept-package-agreements --accept-source-agreements 2>&1") + "\n";
        }
        CheckAll();
        return log;
    }
}
