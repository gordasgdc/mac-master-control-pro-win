namespace MacMasterControlPro.Core.Services;

/// Oglinda NetworkService.swift (Mac) — tuning TCP prin PowerShell/netsh
/// in loc de sysctl/networksetup.
public sealed class NetworkService
{
    public List<string> Adapters { get; private set; } = new();

    /// Scanare libera - permisa si in Trial.
    public void ScanAdapters()
    {
        var raw = Shell.Run("Get-NetAdapter | Where-Object Status -eq 'Up' | Select-Object -ExpandProperty Name");
        Adapters = raw.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
    }

    /// Actiune reala - necesita licenta activata (poarta de Trial in UI).
    /// Aplica DNS pe FIECARE adaptor bifat (Bara de Actiune in Masa, regula
    /// globala de multi-selectie 2026-08-30) - setarile TCP globale ruleaza
    /// o singura data, nu per adaptor.
    public bool ApplyTuning(IEnumerable<string> selectedAdapters)
    {
        var commands = new List<string>
        {
            "netsh interface tcp set global autotuninglevel=normal",
            "netsh interface tcp set global rss=enabled",
            "netsh interface tcp set global chimney=enabled",
        };
        commands.AddRange(selectedAdapters.Select(adapter =>
            $"Set-DnsClientServerAddress -InterfaceAlias '{adapter}' -ServerAddresses ('1.1.1.1','8.8.8.8')"));
        return PrivilegedRunner.Run(commands);
    }
}
