namespace MacMasterControlPro.Core.Services;

/// Oglinda NetworkService.swift (Mac) — tuning TCP prin PowerShell/netsh
/// in loc de sysctl/networksetup.
public sealed class NetworkService
{
    public List<string> Adapters { get; private set; } = new();
    public string SelectedAdapter { get; set; } = "";

    /// Scanare libera - permisa si in Trial.
    public void ScanAdapters()
    {
        var raw = Shell.Run("Get-NetAdapter | Where-Object Status -eq 'Up' | Select-Object -ExpandProperty Name");
        Adapters = raw.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
        if (SelectedAdapter == "" && Adapters.Count > 0) SelectedAdapter = Adapters[0];
    }

    /// Actiune reala - necesita licenta activata (poarta de Trial in UI).
    public bool ApplyTuning()
    {
        var commands = new[]
        {
            "netsh interface tcp set global autotuninglevel=normal",
            "netsh interface tcp set global rss=enabled",
            "netsh interface tcp set global chimney=enabled",
            $"Set-DnsClientServerAddress -InterfaceAlias '{SelectedAdapter}' -ServerAddresses ('1.1.1.1','8.8.8.8')",
        };
        return PrivilegedRunner.Run(commands);
    }
}
