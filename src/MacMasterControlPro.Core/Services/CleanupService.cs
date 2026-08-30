using System.Runtime.InteropServices;

namespace MacMasterControlPro.Core.Services;

/// Oglinda CleanupService.swift (Mac) — cai Windows in loc de ~/Library.
public sealed class CleanupService
{
    [DllImport("psapi.dll")]
    private static extern bool EmptyWorkingSet(IntPtr hProcess);

    private static readonly string[] ReclaimablePaths =
    {
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Blackmagic Design", "DaVinci Resolve", "CacheClip"),
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Adobe", "Common", "Media Cache Files"),
        Path.GetTempPath(),
    };

    /// Scanare libera (Trial) - calculeaza GB recuperabile fara sa stearga nimic.
    public string ScanReclaimable()
    {
        var lines = new List<string>();
        long totalBytes = 0;
        foreach (var path in ReclaimablePaths)
        {
            var size = DirectorySize(path);
            totalBytes += size;
            lines.Add($"• {Path.GetFileName(path.TrimEnd(Path.DirectorySeparatorChar))}: {size / 1024.0 / 1024.0 / 1024.0:F2} GB");
        }
        lines.Insert(0, $"Total recuperabil: {totalBytes / 1024.0 / 1024.0 / 1024.0:F2} GB");
        return string.Join("\n", lines);
    }

    private static long DirectorySize(string path)
    {
        if (!Directory.Exists(path)) return 0;
        try
        {
            return new DirectoryInfo(path).EnumerateFiles("*", SearchOption.AllDirectories).Sum(f => f.Length);
        }
        catch { return 0; }
    }

    /// Actiune reala - fisiere proprii, fara UAC.
    public void CleanMediaCaches()
    {
        foreach (var path in ReclaimablePaths.Take(2))
        {
            DeleteContents(path);
        }
    }

    private static void DeleteContents(string path)
    {
        if (!Directory.Exists(path)) return;
        try
        {
            foreach (var file in Directory.EnumerateFiles(path)) File.Delete(file);
            foreach (var dir in Directory.EnumerateDirectories(path)) Directory.Delete(dir, true);
        }
        catch { /* fisiere in uz, ignoram individual */ }
    }

    /// EmptyWorkingSet cere doar handle-ul procesului curent, fara UAC.
    public void PurgeRamAndFlushDns()
    {
        foreach (var process in System.Diagnostics.Process.GetProcesses())
        {
            try { EmptyWorkingSet(process.Handle); } catch { /* procese de sistem, fara acces */ }
        }
        Shell.Run("ipconfig /flushdns");
    }
}
