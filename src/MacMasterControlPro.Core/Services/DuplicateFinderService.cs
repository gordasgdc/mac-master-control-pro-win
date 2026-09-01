using System.Security.Cryptography;
using System.Text.Json;

namespace MacMasterControlPro.Core.Services;

/// Oglinda DuplicateScanFolders.swift (Mac) — persista folderele alese
/// pentru cautarea de duplicate (`%LocalAppData%\MacMasterControlPro\
/// duplicates-folders.json`). Fara implicite bifate — o scanare pe tot
/// discul poate dura mult, userul alege explicit unde cauta.
public sealed class DuplicateScanFolders
{
    public static readonly DuplicateScanFolders Shared = new();

    private static readonly string ConfigPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "MacMasterControlPro", "duplicates-folders.json");

    public List<string> Folders { get; private set; } = new();

    private DuplicateScanFolders()
    {
        try
        {
            if (File.Exists(ConfigPath))
                Folders = JsonSerializer.Deserialize<List<string>>(File.ReadAllText(ConfigPath)) ?? new();
        }
        catch { Folders = new(); }
    }

    public void AddFolder(string path)
    {
        if (Folders.Contains(path, StringComparer.OrdinalIgnoreCase)) return;
        Folders.Add(path);
        Persist();
    }

    public void RemoveFolder(string path)
    {
        Folders.RemoveAll(f => string.Equals(f, path, StringComparison.OrdinalIgnoreCase));
        Persist();
    }

    private void Persist()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(ConfigPath)!);
            File.WriteAllText(ConfigPath, JsonSerializer.Serialize(Folders));
        }
        catch { /* nescriere nu trebuie sa blocheze UI-ul */ }
    }
}

public sealed record DuplicateFile(string Path, long SizeBytes, DateTime? ModifiedDate)
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

public sealed record DuplicateGroup(string Hash, List<DuplicateFile> Files)
{
    public long SizeBytes => Files.Count > 0 ? Files[0].SizeBytes : 0;
    public long ReclaimableBytes => SizeBytes * (Files.Count - 1);
}

/// Oglinda DuplicateFinderService.swift (Mac) — cerinta directa (Cristi,
/// 2026-09-01): "sa caute duplicatele ... sa vizualizez ... inainte de a
/// alege care vreau sa sterg". Grupare STRICT pe hash de continut
/// (SHA256), niciodata doar pe nume/data — doua fisiere cu acelasi nume
/// si aceeasi data dar continut diferit NU apar niciodata ca duplicate.
public static class DuplicateFinderService
{
    public static List<DuplicateGroup> Scan(IEnumerable<string> roots, long minimumBytes = 1024, Action<string>? progress = null)
    {
        // Etapa 1 (ieftina): grupeaza pe dimensiune - fisierele cu
        // dimensiuni unice n-au cum sa fie duplicate, eliminate fara sa
        // le citim deloc. Etapa 2 (scumpa): hash SHA256 doar pe restul.
        var bySize = new Dictionary<long, List<string>>();
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
                if (size < minimumBytes) continue;
                if (!bySize.TryGetValue(size, out var list)) { list = new(); bySize[size] = list; }
                list.Add(file);
            }
        }

        var groups = new List<DuplicateGroup>();
        var done = 0;
        foreach (var (_, paths) in bySize)
        {
            if (paths.Count < 2) continue;
            var byHash = new Dictionary<string, List<string>>();
            foreach (var path in paths)
            {
                done++;
                progress?.Invoke($"Verificare ({done}): {System.IO.Path.GetFileName(path)}");
                var hash = Sha256OfFile(path);
                if (hash == null) continue;
                if (!byHash.TryGetValue(hash, out var list)) { list = new(); byHash[hash] = list; }
                list.Add(path);
            }
            foreach (var (hash, samePaths) in byHash)
            {
                if (samePaths.Count < 2) continue;
                var files = samePaths.Select(p =>
                {
                    var info = new FileInfo(p);
                    return new DuplicateFile(p, info.Length, info.Exists ? info.LastWriteTime : null);
                }).ToList();
                groups.Add(new DuplicateGroup(hash, files));
            }
        }
        return groups.OrderByDescending(g => g.ReclaimableBytes).ToList();
    }

    /// Citit in bucati fixe (Regula 21 - zero acumulare in memorie pe
    /// fisiere mari), nu File.ReadAllBytes dintr-o singura bucata.
    private static string? Sha256OfFile(string path)
    {
        try
        {
            using var stream = File.OpenRead(path);
            using var sha = SHA256.Create();
            var hash = sha.ComputeHash(stream);
            return Convert.ToHexString(hash).ToLowerInvariant();
        }
        catch { return null; }
    }

    public static void Delete(IEnumerable<DuplicateFile> files, Action<string> log)
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
