using System.Diagnostics;
using System.Management;

namespace MacMasterControlPro.Core.Services;

/// Port 1:1 al DiskHealthService.swift (Mac) - un disc de scratch/cache
/// aproape plin sau pe moarte e cea mai frecventa cauza reala de "Resolve
/// se blocheaza"/randari care esueaza la mijloc.
public sealed class DiskHealth
{
    public required string Name { get; init; }
    public required string DriveLetter { get; init; }
    public required long TotalBytes { get; init; }
    public required long AvailableBytes { get; init; }
    public string? SmartStatus { get; set; }
    public double? WriteSpeedMBps { get; set; }

    public double FreePercent => TotalBytes > 0 ? (double)AvailableBytes / TotalBytes * 100 : 0;
    public bool IsLowSpace => FreePercent < 10;
    public bool IsFailing => SmartStatus != null && SmartStatus.Contains("Pred Fail", StringComparison.OrdinalIgnoreCase);
}

public static class DiskHealthService
{
    public static List<DiskHealth> ScanVolumes()
    {
        var smartByDrive = ReadSmartStatuses();
        var result = new List<DiskHealth>();
        foreach (var drive in DriveInfo.GetDrives())
        {
            if (!drive.IsReady) continue;
            var health = new DiskHealth
            {
                Name = string.IsNullOrEmpty(drive.VolumeLabel) ? drive.Name : drive.VolumeLabel,
                DriveLetter = drive.Name,
                TotalBytes = drive.TotalSize,
                AvailableBytes = drive.AvailableFreeSpace,
            };
            health.SmartStatus = smartByDrive.TryGetValue(drive.Name.TrimEnd('\\'), out var status) ? status : null;
            result.Add(health);
        }
        return result;
    }

    /// SMART prin WMI (`MSStorageDriver_FailurePredictStatus`, namespace
    /// `root\WMI`) - mapat aproximativ la litera de disc via `Win32_DiskDrive`
    /// + `Win32_DiskDriveToDiskPartition` + `Win32_LogicalDiskToPartition`.
    /// Poate returna gol pe discuri externe USB (nu toate expun SMART prin
    /// puntea USB) - degradeaza elegant, nu e o eroare.
    private static Dictionary<string, string> ReadSmartStatuses()
    {
        var map = new Dictionary<string, string>();
        try
        {
            using var searcher = new ManagementObjectSearcher(@"root\WMI", "SELECT * FROM MSStorageDriver_FailurePredictStatus");
            foreach (ManagementObject obj in searcher.Get())
            {
                var predictFailure = (bool)(obj["PredictFailure"] ?? false);
                var instanceName = obj["InstanceName"]?.ToString() ?? "";
                map[instanceName] = predictFailure ? "Pred Fail" : "Verified";
            }
        }
        catch
        {
            // WMI SMART indisponibil (drivere/permisiuni) - degradeaza elegant.
        }
        return map;
    }

    /// BUG REAL (oglinda fix-ului Mac, 2026-08-31: "apas Testeaza viteza si
    /// nu se intampla nimic") - scrierea putea esua silentios (disc plin,
    /// litera de sistem fara drept de scriere direct la radacina pentru
    /// userul curent) si `null` nu ajungea niciodata vizibil in UI. Acum
    /// returnam mesajul de eroare explicit, si scriem in discul de sistem
    /// (C:\) in %TEMP% (aceeasi unitate fizica, dar zona mereu scriabila),
    /// nu la radacina.
    public static (double? Speed, string? Error) MeasureWriteSpeed(string driveLetter)
    {
        var isSystemDrive = string.Equals(Path.GetPathRoot(Environment.GetFolderPath(Environment.SpecialFolder.Windows)), driveLetter, StringComparison.OrdinalIgnoreCase);
        var writeDir = isSystemDrive ? Path.GetTempPath() : driveLetter;
        var testFile = Path.Combine(writeDir, $".mmc_speedtest_{Guid.NewGuid()}.bin");
        const int sizeBytes = 256 * 1024 * 1024;
        var data = new byte[sizeBytes];
        Random.Shared.NextBytes(data);
        var sw = Stopwatch.StartNew();
        try
        {
            File.WriteAllBytes(testFile, data);
        }
        catch (Exception ex)
        {
            return (null, $"Scrierea a eșuat: {ex.Message}");
        }
        sw.Stop();
        try { File.Delete(testFile); } catch { /* best-effort */ }
        if (sw.Elapsed.TotalSeconds <= 0) return (null, "Măsurătoarea a fost prea rapidă ca să fie exactă — încearcă din nou.");
        return ((sizeBytes / 1_048_576.0) / sw.Elapsed.TotalSeconds, null);
    }
}
