namespace MacMasterControlPro.Core.Services;

/// [2026-09-04] Port 1:1 al DiskScanEngine.swift (Mac, v2.30.0) — vezi
/// acel fișier pentru raționamentul complet al arhitecturii. Rezumat:
/// modulul „Analiză Disc" nu exista deloc pe Windows (doar
/// `DiskHealthService`, complet diferit — sănătate SMART, nu explorare de
/// spațiu) — construit de la zero, cu aceleași trei mecanisme cerute
/// explicit de Cristi pentru Mac, adaptate la .NET/Windows:
///
/// 1. **Enumerare nativă, nu recursivitate PowerShell/`Get-ChildItem`.**
///    `DirectoryInfo.EnumerateFileSystemInfos()` cheamă intern API-ul
///    Win32 de nivel jos (`FindFirstFile`/`FindNextFile`) — o SINGURĂ
///    trecere pe director dă simultan nume, mărime ȘI dată de
///    modificare, fără `stat()`-uri separate per fișier (echivalentul
///    direct al `fts_statp` de pe Mac). Alternativa „adevărat nivel jos"
///    (citire directă MFT) a fost respinsă pentru ACELAȘI motiv ca pe
///    Mac (`getattrlistbulk` respins în favoarea `fts`): complexitate și
///    risc disproporționate (specific NTFS, cere Administrator, nu merge
///    pe FAT32/exFAT/ReFS/unități de rețea) față de câștigul real.
/// 2. **Paralel pe toate nucleele.** `Parallel.For` peste subfolderele de
///    PRIM NIVEL ale rădăcinii — echivalentul .NET al
///    `DispatchQueue.concurrentPerform` de pe Mac. Fiecare thread
///    construiește propriul subarbore, ZERO stare comună mutabilă în
///    timpul scanării.
/// 3. **Scanare incrementală.** `IncrementalUpdate` compară mtime-ul
///    fiecărui folder cu cel din cache (`DiskNode.DirectoryModifiedAtTicks`)
///    — port direct al strategiei Mac, EXPLICIT portabilă pe Windows prin
///    `Directory.GetLastWriteTimeUtc` (menționată ca atare încă din
///    jurnalul Mac).
///
/// **Capcană reală de platformă, verificată explicit înainte de a scrie
/// codul de mai jos** (Regula 30 — nu presupunem): spre deosebire de
/// `stat()` pe Mac (eșuează cu un cod de eroare pe o cale inexistentă),
/// `Directory.GetLastWriteTimeUtc` pe .NET NU aruncă pe o cale lipsă —
/// întoarce tăcut sentinela `1601-01-01` (`DateTime.FromFileTimeUtc(0)`).
/// Orice verificare „a dispărut folderul de pe disc?" trebuie să
/// folosească explicit `Directory.Exists`, NICIODATĂ să se bazeze pe o
/// excepție de la `GetLastWriteTimeUtc` — vezi `SafeDirectoryMTime`/
/// `RefreshDirectory` mai jos.
public static class DiskScanEngine
{
    public sealed class Progress
    {
        public int FilesIndexed { get; init; }
        public long BytesIndexed { get; init; }
    }

    private sealed record TopLevelEntry(string Name, string Path, bool IsDirectory, long Size);

    /// Acumulator de progres thread-safe — raportează în loturi (nu
    /// per-fișier), doar rezultatul agregat, throttled la 1/secundă,
    /// ajunge la `onProgress`. Portul Windows nu are nevoie să forțeze
    /// dispatch pe thread de UI aici — apelantul (pagina WPF) e cel care
    /// face `Dispatcher.Invoke` în jurul callback-ului, la fel ca restul
    /// serviciilor din acest Core (`DuplicateFinderService.Scan`).
    private sealed class ProgressCounter
    {
        private readonly object _lock = new();
        private int _files;
        private long _bytes;
        private DateTime _lastReportedAt = DateTime.UtcNow;

        public int Files { get { lock (_lock) return _files; } }
        public long Bytes { get { lock (_lock) return _bytes; } }

        public void Add(int deltaFiles, long deltaBytes, Action<Progress> onProgress)
        {
            Progress? snapshot = null;
            lock (_lock)
            {
                _files += deltaFiles;
                _bytes += deltaBytes;
                var now = DateTime.UtcNow;
                if ((now - _lastReportedAt).TotalSeconds >= 1)
                {
                    _lastReportedAt = now;
                    snapshot = new Progress { FilesIndexed = _files, BytesIndexed = _bytes };
                }
            }
            if (snapshot is not null) onProgress(snapshot);
        }
    }

    // MARK: - Scanare completa (prima data, sau "Reseteaza cache")

    /// Construiește arborele complet pentru `root`, paralel pe toate
    /// nucleele. Rulează SINCRON pe thread-ul apelantului — apelantul
    /// (pagina WPF) îl pornește prin `Task.Run`, la fel ca
    /// `DuplicateFinderService.Scan`.
    public static DiskNode BuildTree(string root, Action<Progress> onProgress)
    {
        var resolvedRoot = System.IO.Path.TrimEndingDirectorySeparator(System.IO.Path.GetFullPath(root));
        var displayName = System.IO.Path.GetFileName(resolvedRoot);
        var rootNode = new DiskNode(string.IsNullOrEmpty(displayName) ? resolvedRoot : displayName, resolvedRoot, isDirectory: true)
        {
            DirectoryModifiedAtTicks = SafeDirectoryMTime(resolvedRoot),
        };

        List<TopLevelEntry> topLevel;
        try { topLevel = DirectChildren(resolvedRoot); }
        catch { return rootNode; }
        if (topLevel.Count == 0) return rootNode;

        var progressReporter = new ProgressCounter();
        var partial = new DiskNode?[topLevel.Count];

        Parallel.For(0, topLevel.Count, i =>
        {
            var entry = topLevel[i];
            partial[i] = entry.IsDirectory
                ? ScanSubtreeNative(entry.Path, entry.Name, progressReporter, onProgress)
                : NewFileNode(entry, progressReporter, onProgress);
        });

        foreach (var node in partial)
        {
            if (node is null) continue;
            rootNode.Children[node.Name] = node;
            rootNode.SizeBytes += node.SizeBytes;
        }

        onProgress(new Progress { FilesIndexed = progressReporter.Files, BytesIndexed = progressReporter.Bytes });
        return rootNode;
    }

    private static DiskNode NewFileNode(TopLevelEntry entry, ProgressCounter progressReporter, Action<Progress> onProgress)
    {
        var node = new DiskNode(entry.Name, entry.Path, isDirectory: false) { SizeBytes = entry.Size };
        progressReporter.Add(1, entry.Size, onProgress);
        return node;
    }

    /// Enumerare recursivă, completă, a UNUI subfolder — apelată o dată
    /// per subfolder de prim nivel (paralel, vezi `BuildTree`), și din
    /// nou, punctual, de `RefreshDirectory` pentru un folder nou-apărut
    /// care nu exista deloc în cache. Recursivitate simplă (nu o stivă
    /// manuală) — adâncimi de foldere de mii de niveluri sunt nerealiste
    /// în practică, riscul de stack overflow e neglijabil.
    private static DiskNode ScanSubtreeNative(string root, string name, ProgressCounter progressReporter, Action<Progress> onProgress)
    {
        var rootNode = new DiskNode(name, root, isDirectory: true) { DirectoryModifiedAtTicks = SafeDirectoryMTime(root) };
        int pendingFiles = 0;
        long pendingBytes = 0;

        void Walk(string dirPath, DiskNode dirNode)
        {
            IEnumerable<FileSystemInfo> entries;
            try { entries = new DirectoryInfo(dirPath).EnumerateFileSystemInfos(); }
            catch { return; } // acces refuzat/disparut intre timp - degradeaza elegant, nu opreste tot scanul

            foreach (var entry in entries)
            {
                bool isDir = (entry.Attributes & FileAttributes.Directory) != 0;
                if (isDir)
                {
                    long mtimeTicks;
                    try { mtimeTicks = entry.LastWriteTimeUtc.Ticks; } catch { mtimeTicks = 0; }
                    var childNode = new DiskNode(entry.Name, entry.FullName, isDirectory: true) { DirectoryModifiedAtTicks = mtimeTicks };
                    dirNode.Children[entry.Name] = childNode;
                    Walk(entry.FullName, childNode);
                    dirNode.SizeBytes += childNode.SizeBytes;
                }
                else
                {
                    long size;
                    try { size = ((FileInfo)entry).Length; } catch { size = 0; }
                    dirNode.Children[entry.Name] = new DiskNode(entry.Name, entry.FullName, isDirectory: false) { SizeBytes = size };
                    dirNode.SizeBytes += size;
                    pendingFiles++;
                    pendingBytes += size;
                    if (pendingFiles >= 500)
                    {
                        progressReporter.Add(pendingFiles, pendingBytes, onProgress);
                        pendingFiles = 0;
                        pendingBytes = 0;
                    }
                }
            }
        }

        Walk(root, rootNode);
        if (pendingFiles > 0) progressReporter.Add(pendingFiles, pendingBytes, onProgress);
        return rootNode;
    }

    // MARK: - Scanare incrementala ("Re-scaneaza doar modificarile")

    public static DiskNode IncrementalUpdate(DiskNode cachedRoot, Action<Progress> onProgress)
    {
        var progressReporter = new ProgressCounter();
        var updated = RefreshDirectory(cachedRoot, progressReporter, onProgress);
        onProgress(new Progress { FilesIndexed = progressReporter.Files, BytesIndexed = progressReporter.Bytes });
        return updated;
    }

    /// Recursiv, dar ieftin: costul real e un `GetLastWriteTimeUtc` per
    /// folder vizitat — un subarbore ale cărui mtime-uri n-au fost atinse
    /// tot recursează prin el (ca să verifice și sub-subfolderele), dar
    /// NICIODATĂ nu re-listează conținutul unui folder al cărui mtime
    /// propriu n-a diferit.
    private static DiskNode RefreshDirectory(DiskNode node, ProgressCounter progressReporter, Action<Progress> onProgress)
    {
        if (!node.IsDirectory) return node;

        // Vezi capcana documentata la nivelul clasei: `Directory.Exists`
        // explicit, NICIODATA doar o exceptie de la GetLastWriteTimeUtc.
        if (!Directory.Exists(node.Path))
        {
            node.Children.Clear();
            node.SizeBytes = 0;
            return node;
        }
        var currentMTime = SafeDirectoryMTime(node.Path);

        if (currentMTime == node.DirectoryModifiedAtTicks)
        {
            foreach (var child in node.Children.Values.Where(c => c.IsDirectory))
                RefreshDirectory(child, progressReporter, onProgress);
            node.SizeBytes = node.Children.Values.Sum(c => c.SizeBytes);
            return node;
        }

        node.DirectoryModifiedAtTicks = currentMTime;
        List<TopLevelEntry> liveEntries;
        try { liveEntries = DirectChildren(node.Path); } catch { liveEntries = new(); }
        var liveNames = new HashSet<string>(liveEntries.Select(e => e.Name), StringComparer.OrdinalIgnoreCase);

        foreach (var staleName in node.Children.Keys.Where(k => !liveNames.Contains(k)).ToList())
            node.Children.Remove(staleName);

        int newFiles = 0;
        long newBytes = 0;
        foreach (var entry in liveEntries)
        {
            if (entry.IsDirectory)
            {
                if (node.Children.TryGetValue(entry.Name, out var existingChild))
                    RefreshDirectory(existingChild, progressReporter, onProgress);
                else
                    node.Children[entry.Name] = ScanSubtreeNative(entry.Path, entry.Name, progressReporter, onProgress);
            }
            else if (!node.Children.TryGetValue(entry.Name, out var existingFile) || existingFile.SizeBytes != entry.Size)
            {
                node.Children[entry.Name] = new DiskNode(entry.Name, entry.Path, isDirectory: false) { SizeBytes = entry.Size };
                newFiles++;
                newBytes += entry.Size;
            }
        }
        if (newFiles > 0) progressReporter.Add(newFiles, newBytes, onProgress);
        node.SizeBytes = node.Children.Values.Sum(c => c.SizeBytes);
        return node;
    }

    // MARK: - Stergere (actualizeaza arborele fara rescanare)

    /// Actualizează arborele DUPĂ o ștergere reușită — scade mărimea
    /// fișierului/folderului șters din TOȚI strămoșii lui și îl elimină
    /// din arbore, ca navigarea să rămână instantă.
    public static void Remove(string nodePath, DiskNode root)
    {
        var rootPrefix = root.Path.EndsWith(System.IO.Path.DirectorySeparatorChar)
            ? root.Path
            : root.Path + System.IO.Path.DirectorySeparatorChar;
        if (!nodePath.StartsWith(rootPrefix, StringComparison.OrdinalIgnoreCase)) return;
        var relative = nodePath[rootPrefix.Length..];
        var components = relative.Split(System.IO.Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries);
        if (components.Length == 0) return;

        var chain = new List<DiskNode> { root };
        var current = root;
        foreach (var component in components)
        {
            if (!current.Children.TryGetValue(component, out var child)) return;
            chain.Add(child);
            current = child;
        }
        var removedSize = current.SizeBytes;
        for (int i = 0; i < chain.Count - 1; i++) chain[i].SizeBytes -= removedSize;
        chain[^2].Children.Remove(components[^1]);
    }

    // MARK: - Helpers native

    /// Listare de UN SINGUR nivel — pentru împărțirea în task-uri paralele
    /// (subfolderele de prim nivel ale rădăcinii) și pentru comparația
    /// "ce mai există pe disc" din scanarea incrementală.
    private static List<TopLevelEntry> DirectChildren(string path)
    {
        var result = new List<TopLevelEntry>();
        foreach (var entry in new DirectoryInfo(path).EnumerateFileSystemInfos())
        {
            bool isDir = (entry.Attributes & FileAttributes.Directory) != 0;
            long size = 0;
            if (!isDir)
            {
                try { size = ((FileInfo)entry).Length; } catch { /* fisier inaccesibil - marime 0, mai bine decat crash */ }
            }
            result.Add(new TopLevelEntry(entry.Name, entry.FullName, isDir, size));
        }
        return result;
    }

    private static long SafeDirectoryMTime(string path)
    {
        try { return Directory.Exists(path) ? Directory.GetLastWriteTimeUtc(path).Ticks : 0; }
        catch { return 0; }
    }
}
