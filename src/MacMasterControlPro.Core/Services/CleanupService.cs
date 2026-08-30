using System.Runtime.InteropServices;

namespace MacMasterControlPro.Core.Services;

public sealed class CleanableItem
{
    public string Id => Path;
    public required string Name { get; init; }
    public required string Path { get; init; }
    public long SizeBytes { get; init; }
    public double SizeGB => SizeBytes / 1024.0 / 1024.0 / 1024.0;
}

/// Oglinda CleanupService.swift (Mac) — cai Windows in loc de ~/Library.
/// Restructurat pentru selectie granulara (checkbox per item).
public sealed class CleanupService
{
    [DllImport("psapi.dll")]
    private static extern bool EmptyWorkingSet(IntPtr hProcess);

    public List<CleanableItem> Items { get; private set; } = new();

    private static readonly (string name, string path)[] CachePaths =
    {
        ("DaVinci Resolve CacheClip", Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Blackmagic Design", "DaVinci Resolve", "CacheClip")),
        ("Adobe Media Cache", Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Adobe", "Common", "Media Cache Files")),
        ("Fișiere temporare (%Temp%)", Path.GetTempPath()),
    };

    /// Scanare libera (Trial) - calculeaza GB per item, fara sa stearga nimic.
    public List<CleanableItem> ScanReclaimable()
    {
        Items = CachePaths.Select(entry => new CleanableItem
        {
            Name = entry.name,
            Path = entry.path,
            SizeBytes = DirectorySize(entry.path),
        }).ToList();
        return Items;
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

    /// Actiune reala - sterge DOAR itemii bifati de utilizator, fara UAC.
    /// `log` primeste o linie per fisier/folder (panou "terminal live" in UI)
    /// - fara el, o eroare pe PRIMUL fisier bloca silentios TOATA stergerea
    /// (bug real, gasit 2026-08-30: catch-ul original invelea toata bucla,
    /// nu fiecare fisier in parte, deci un singur fisier "in uz" oprea orice
    /// progres fara nicio urma vizibila pentru user).
    public void DeleteSelected(IEnumerable<CleanableItem> selected, Action<string>? log = null)
    {
        foreach (var item in selected)
        {
            log?.Invoke($"Ștergere: {item.Name}…");
            var (deleted, skipped) = DeleteContents(item.Path, log);
            log?.Invoke($"  ✔ {item.Name}: {deleted} șterse, {skipped} sărite (în uz/blocate).");
        }
        ScanReclaimable();
    }

    private static (int deleted, int skipped) DeleteContents(string path, Action<string>? log)
    {
        if (!Directory.Exists(path)) return (0, 0);
        var deleted = 0;
        var skipped = 0;
        foreach (var file in Directory.EnumerateFiles(path))
        {
            try { File.Delete(file); deleted++; }
            catch (Exception ex) { skipped++; log?.Invoke($"    blocat: {Path.GetFileName(file)} ({ex.GetType().Name})"); }
        }
        foreach (var dir in Directory.EnumerateDirectories(path))
        {
            try { Directory.Delete(dir, true); deleted++; }
            catch (Exception ex) { skipped++; log?.Invoke($"    blocat: {Path.GetFileName(dir)}/ ({ex.GetType().Name})"); }
        }
        return (deleted, skipped);
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
