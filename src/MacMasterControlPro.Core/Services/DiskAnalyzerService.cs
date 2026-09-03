namespace MacMasterControlPro.Core.Services;

/// Port 1:1 al DiskAnalyzerService.swift (Mac) — doar partea de radacini
/// initiale a supravietuit acolo dupa v2.30.0 (restul a fost inlocuit de
/// DiskScanEngine); aici, la fel, doar `AvailableRoots`.
public sealed record DiskEntry(string Name, string Path, long SizeBytes)
{
    public string SizeDescription => DiskNode.FormatBytes(SizeBytes);
}

public static class DiskAnalyzerServiceRoots
{
    /// Rădăcinile inițiale oferite userului — toate unitățile GATA
    /// (`IsReady`, exclude un cititor de CD/card gol), la fel ca
    /// `DiskHealthService.ScanVolumes()`.
    public static List<DiskEntry> AvailableRoots()
    {
        var result = new List<DiskEntry>();
        foreach (var drive in DriveInfo.GetDrives())
        {
            if (!drive.IsReady) continue;
            var name = string.IsNullOrEmpty(drive.VolumeLabel) ? drive.Name : $"{drive.VolumeLabel} ({drive.Name.TrimEnd('\\')})";
            result.Add(new DiskEntry(name, drive.RootDirectory.FullName, drive.TotalSize));
        }
        return result;
    }
}
