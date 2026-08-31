using System.Diagnostics;
using System.IO.Compression;

namespace MacMasterControlPro.Core.Services;

/// Oglinda ResolveDatabaseBackupService.swift (Mac) — locatia implicita
/// "Disk Database" pe Windows e in ProgramData, nu AppData (Resolve o
/// trateaza ca resursa partajata la nivel de masina, nu per-user).
public sealed record ResolveBackupEntry(string Path, DateTime Date, long SizeBytes)
{
    public string Name => System.IO.Path.GetFileName(Path);
}

public static class ResolveDatabaseBackupService
{
    public static string DatabasePath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
        "Blackmagic Design", "DaVinci Resolve", "Resolve Disk Database", "Resolve Projects");

    private static string BackupsDirectory => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.MyVideos), "MacMasterControlPro-ResolveBackups");

    public static bool DatabaseExists() => Directory.Exists(DatabasePath);

    public static long DatabaseSizeBytes()
    {
        if (!DatabaseExists()) return 0;
        try { return new DirectoryInfo(DatabasePath).EnumerateFiles("*", SearchOption.AllDirectories).Sum(f => f.Length); }
        catch { return 0; }
    }

    public static bool IsResolveRunning() => Process.GetProcessesByName("Resolve").Length > 0;

    public static List<ResolveBackupEntry> ListBackups()
    {
        if (!Directory.Exists(BackupsDirectory)) return new();
        return Directory.GetFiles(BackupsDirectory, "*.zip")
            .Select(p => new ResolveBackupEntry(p, File.GetCreationTime(p), new FileInfo(p).Length))
            .OrderByDescending(b => b.Date)
            .ToList();
    }

    /// Resolve TREBUIE inchis inainte de backup — la fel ca pe Mac, o
    /// copiere "la cald" a bazei de date poate prinde fisiere in scriere.
    public static string CreateBackup()
    {
        if (IsResolveRunning())
            throw new InvalidOperationException("Închide DaVinci Resolve înainte de backup — copierea „la cald” poate corupe arhiva.");
        if (!DatabaseExists())
            throw new InvalidOperationException("Nu am găsit baza de date de proiecte a Resolve pe acest PC.");

        Directory.CreateDirectory(BackupsDirectory);
        var destination = Path.Combine(BackupsDirectory, $"ResolveProjectLibrary-{DateTime.Now:yyyy-MM-dd_HH-mm-ss}.zip");
        ZipFile.CreateFromDirectory(DatabasePath, destination, CompressionLevel.Fastest, includeBaseDirectory: true);
        return destination;
    }

    public static void RevealInExplorer(string path) =>
        Process.Start("explorer.exe", $"/select,\"{path}\"");

    /// "Resolve zombie": proces activ dar fara nicio fereastra principala
    /// vizibila (`MainWindowHandle == IntPtr.Zero`) — acelasi tipar deja
    /// verificat in MediaFlow Monitor (`ProcessInspector.cs`).
    public static bool IsResolveZombie()
    {
        var processes = Process.GetProcessesByName("Resolve");
        if (processes.Length == 0) return false;
        return processes.All(p => p.MainWindowHandle == IntPtr.Zero);
    }

    public static void ForceQuitResolve()
    {
        foreach (var process in Process.GetProcessesByName("Resolve"))
        {
            try { process.Kill(); } catch { /* deja iesit */ }
        }
    }
}
