namespace MacMasterControlPro.Core.Services;

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
public static class BigFileFinderService
{
    private static IEnumerable<string> ScanRoots()
    {
        var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        yield return Path.Combine(userProfile, "Downloads");
        yield return Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
        yield return Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        yield return Environment.GetFolderPath(Environment.SpecialFolder.MyVideos);
    }

    public static List<BigFileWin> Scan(int minimumMB = 200, int limit = 100)
    {
        var minimumBytes = (long)minimumMB * 1024 * 1024;
        var results = new List<BigFileWin>();
        foreach (var root in ScanRoots())
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
