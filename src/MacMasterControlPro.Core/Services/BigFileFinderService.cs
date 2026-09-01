using System.Text.Json;

namespace MacMasterControlPro.Core.Services;

/// Oglinda BigFileScanFolders.swift (Mac) — persista selectia de foldere
/// pentru scanarea de fisiere mari (`%LocalAppData%\MacMasterControlPro\
/// bigfiles-folders.json`), ca userul sa nu retrebuiasca sa o refaca la
/// fiecare deschidere.
public sealed class BigFileScanFolders
{
    public static readonly BigFileScanFolders Shared = new();

    private static readonly string ConfigPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "MacMasterControlPro", "bigfiles-folders.json");

    private sealed record Persisted(List<string> EnabledDefaults, List<string> CustomFolders);

    public HashSet<string> EnabledDefaults { get; private set; }
    public List<string> CustomFolders { get; private set; }

    private BigFileScanFolders()
    {
        var defaults = BigFileFinderService.DefaultRoots();
        try
        {
            if (File.Exists(ConfigPath))
            {
                var loaded = JsonSerializer.Deserialize<Persisted>(File.ReadAllText(ConfigPath));
                if (loaded != null)
                {
                    EnabledDefaults = new HashSet<string>(loaded.EnabledDefaults, StringComparer.OrdinalIgnoreCase);
                    CustomFolders = loaded.CustomFolders;
                    return;
                }
            }
        }
        catch { /* fisier corupt/lipsa - pornim de la implicit */ }
        EnabledDefaults = new HashSet<string>(defaults, StringComparer.OrdinalIgnoreCase); // toate bifate implicit
        CustomFolders = new List<string>();
    }

    public List<string> ActiveRoots() =>
        BigFileFinderService.DefaultRoots().Where(EnabledDefaults.Contains).Concat(CustomFolders).ToList();

    public void ToggleDefault(string path)
    {
        if (!EnabledDefaults.Add(path)) EnabledDefaults.Remove(path);
        Persist();
    }

    public void AddCustomFolder(string path)
    {
        if (CustomFolders.Contains(path, StringComparer.OrdinalIgnoreCase)) return;
        if (BigFileFinderService.DefaultRoots().Contains(path, StringComparer.OrdinalIgnoreCase)) return;
        CustomFolders.Add(path);
        Persist();
    }

    public void RemoveCustomFolder(string path)
    {
        CustomFolders.RemoveAll(f => string.Equals(f, path, StringComparison.OrdinalIgnoreCase));
        Persist();
    }

    private void Persist()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(ConfigPath)!);
            File.WriteAllText(ConfigPath, JsonSerializer.Serialize(new Persisted(EnabledDefaults.ToList(), CustomFolders)));
        }
        catch { /* nescriere nu trebuie sa blocheze UI-ul */ }
    }
}

public sealed record BigFileWin(string Path, long SizeBytes)
{
    public string Name => System.IO.Path.GetFileName(Path);
    public string SizeDescription
    {
        get
        {
            double b = SizeBytes;
            string[] units = { "B", "KB", "MB", "GB" };
            int i = 0;
            while (b >= 1024 && i < units.Length - 1) { b /= 1024; i++; }
            return $"{b:0.#} {units[i]}";
        }
    }
}

/// Oglinda BigFileFinderService.swift (Mac) — scaneaza Downloads/Desktop/
/// Documents/Videos, folderele unde userii acumuleaza fisiere uitate.
///
/// BUG REAL/cerinta (oglinda fix-ului Mac, 2026-08-31: "se duce singur in
/// toate astea, nu pot sa selectez eu ce vreau sa scanez") — folderele
/// implicite raman `DefaultRoots` (bifate implicit), dar `Scan(roots:)`
/// accepta acum orice lista aleasa explicit de user — vezi
/// `BigFileScanFolders` pentru persistarea selectiei.
public static class BigFileFinderService
{
    public static List<string> DefaultRoots()
    {
        var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        return new List<string>
        {
            Path.Combine(userProfile, "Downloads"),
            Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            Environment.GetFolderPath(Environment.SpecialFolder.MyVideos),
        };
    }

    public static List<BigFileWin> Scan(IEnumerable<string> roots, int minimumMB = 200, int limit = 100)
    {
        var minimumBytes = (long)minimumMB * 1024 * 1024;
        var results = new List<BigFileWin>();
        foreach (var root in roots)
        {
            if (!Directory.Exists(root)) continue;
            IEnumerable<string> files;
            try { files = Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories); }
            catch { continue; }
            foreach (var file in files)
            {
                long size;
                try { size = new FileInfo(file).Length; } catch { continue; }
                if (size >= minimumBytes) results.Add(new BigFileWin(file, size));
            }
        }
        return results.OrderByDescending(f => f.SizeBytes).Take(limit).ToList();
    }

    public static void Delete(IEnumerable<BigFileWin> files, Action<string> log)
    {
        foreach (var file in files)
        {
            try
            {
                Microsoft.VisualBasic.FileIO.FileSystem.DeleteFile(
                    file.Path, Microsoft.VisualBasic.FileIO.UIOption.OnlyErrorDialogs,
                    Microsoft.VisualBasic.FileIO.RecycleOption.SendToRecycleBin);
                log($"Mutat la Coșul de reciclare ({file.SizeDescription}): {file.Path}");
            }
            catch (Exception ex)
            {
                log($"EROARE, nu s-a putut șterge: {file.Path} ({ex.Message})");
            }
        }
    }
}
