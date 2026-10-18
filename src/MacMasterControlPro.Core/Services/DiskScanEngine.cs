using System.Runtime.InteropServices;
using System.Text;

namespace MacMasterControlPro.Core.Services;

/// Motor de analiza disc pentru Windows (nativ .NET, fara administrator, valabil pe orice sistem de fisiere).
///
/// Scanare completa: parcurgere paralela pe subfolderele de prim nivel (EnumerateFileSystemInfos -> FindFirstFileEx,
/// o singura trecere dă nume, marime, atribute, mtime). Nu urmareste puncte de reparse (junction/symlink/OneDrive
/// placeholders ca foldere) si nu descarca fisiere "doar in cloud".
/// Actualizare incrementala `Verified`: relisteaza fiecare folder si compara amprenta fisierelor directe
/// (nume + marime + alocat + mtime) cu cache-ul => detecteaza si modificarea in-loc, la orice adancime.
/// `StructureOnly`: doar mtime de folder (rapid, nu vede modificari in-loc; pentru masuratori).
/// Cititul direct din MFT/USN nu e folosit: cere administrator si nu merge pe ReFS/FAT/exFAT/retea; se reevalueaza
/// doar daca masuratorile arata ca `Verified` e prea lent pe volume NTFS mari.
public static class DiskScanEngine
{
    public sealed class Progress
    {
        public int FilesIndexed { get; init; }
        public long BytesIndexed { get; init; }
        public int AccessErrors { get; init; }
    }

    public enum ScanStatus { Complete, Cancelled, RootUnavailable }
    public enum IncrementalMode { Verified, StructureOnly }

    public sealed record ScanOutcome(ScanStatus Status, int AccessErrors)
    {
        public bool IsCleanAndComplete => Status == ScanStatus.Complete && AccessErrors == 0;
    }

    private sealed record Entry(string Name, string Path, bool IsDirectory, long Size, long Allocated, long MTimeTicks, bool CloudOnly);
    private sealed record Listing(List<Entry> Entries, long DirMTimeTicks, int Errors);

    private sealed class ProgressCounter
    {
        private readonly object _lock = new();
        private int _files, _errors;
        private long _bytes;
        private DateTime _lastReportedAt = DateTime.UtcNow;
        public Progress Snapshot { get { lock (_lock) return new Progress { FilesIndexed = _files, BytesIndexed = _bytes, AccessErrors = _errors }; } }
        public int AccessErrors { get { lock (_lock) return _errors; } }
        public void NoteError(int n = 1) { lock (_lock) _errors += n; }
        public void Add(int files, long bytes, Action<Progress> onProgress)
        {
            Progress? snap = null;
            lock (_lock)
            {
                _files += files; _bytes += bytes;
                var now = DateTime.UtcNow;
                if ((now - _lastReportedAt).TotalSeconds >= 1)
                {
                    _lastReportedAt = now;
                    snap = new Progress { FilesIndexed = _files, BytesIndexed = _bytes, AccessErrors = _errors };
                }
            }
            if (snap is not null) onProgress(snap);
        }
    }

    // MARK: - Scanare completa

    public static (DiskNode Tree, ScanOutcome Outcome) BuildTree(string root, Action<Progress> onProgress, CancellationToken ct = default)
    {
        var op = DiagnosticLog.Begin("disk", "scan.full");
        var resolved = System.IO.Path.TrimEndingDirectorySeparator(System.IO.Path.GetFullPath(root));
        var display = System.IO.Path.GetFileName(resolved);
        var rootNode = new DiskNode(string.IsNullOrEmpty(display) ? resolved : display, resolved, isDirectory: true);
        var counter = new ProgressCounter();
        if (!Directory.Exists(resolved))
        {
            op.End("rootUnavailable", "warning");
            return (rootNode, new ScanOutcome(ScanStatus.RootUnavailable, 0));
        }
        var serial = VolumeSerial(resolved);
        Populate(rootNode, counter, onProgress, parallel: true, ct);
        var status = ct.IsCancellationRequested ? ScanStatus.Cancelled
            : (Directory.Exists(resolved) && VolumeSerial(resolved) == serial ? ScanStatus.Complete : ScanStatus.RootUnavailable);
        var snap = counter.Snapshot;
        op.End(status.ToString(), status == ScanStatus.Complete ? "info" : "warning", fields: new Dictionary<string, string>
        { ["files"] = snap.FilesIndexed.ToString(), ["bytes"] = snap.BytesIndexed.ToString(), ["accessErrors"] = snap.AccessErrors.ToString() });
        onProgress(snap);
        return (rootNode, new ScanOutcome(status, snap.AccessErrors));
    }

    private static void Populate(DiskNode node, ProgressCounter counter, Action<Progress> onProgress, bool parallel, CancellationToken ct)
    {
        if (ct.IsCancellationRequested) return;
        var listing = List(node.Path);
        if (listing is null) { counter.NoteError(); return; }
        counter.NoteError(listing.Errors);
        node.DirectoryModifiedAtTicks = listing.DirMTimeTicks;
        ApplyFiles(listing, node);
        ReportFiles(listing, counter, onProgress);

        var dirs = listing.Entries.Where(e => e.IsDirectory).ToList();
        var built = new DiskNode?[dirs.Count];
        void Work(int i)
        {
            var child = new DiskNode(dirs[i].Name, dirs[i].Path, isDirectory: true);
            Populate(child, counter, onProgress, parallel: false, ct);
            built[i] = child;
        }
        if (parallel) Parallel.For(0, dirs.Count, new ParallelOptions { CancellationToken = CancellationToken.None }, Work);
        else for (var i = 0; i < dirs.Count; i++) Work(i);
        foreach (var c in built) if (c is not null) node.Children[c.Name] = c;
        SumChildren(node);
    }

    // MARK: - Actualizare incrementala

    /// Nu salva rezultatul decat daca `Outcome.Status == Complete`. La `Cancelled` arborele poate fi partial actualizat
    /// (reincarca din cache); la `RootUnavailable` arborele nu a fost modificat.
    public static (DiskNode Tree, ScanOutcome Outcome) IncrementalUpdate(DiskNode cachedRoot, Action<Progress> onProgress,
        IncrementalMode mode = IncrementalMode.Verified, CancellationToken ct = default)
    {
        var op = DiagnosticLog.Begin("disk", "scan.incremental");
        var counter = new ProgressCounter();
        if (!Directory.Exists(cachedRoot.Path))
        {
            op.End("rootUnavailable", "warning");
            return (cachedRoot, new ScanOutcome(ScanStatus.RootUnavailable, 0));
        }
        var serial = VolumeSerial(cachedRoot.Path);
        Refresh(cachedRoot, mode, counter, onProgress, parallel: true, ct);
        var status = ct.IsCancellationRequested ? ScanStatus.Cancelled
            : (Directory.Exists(cachedRoot.Path) && VolumeSerial(cachedRoot.Path) == serial ? ScanStatus.Complete : ScanStatus.RootUnavailable);
        var snap = counter.Snapshot;
        op.End(status.ToString(), status == ScanStatus.Complete ? "info" : "warning", fields: new Dictionary<string, string>
        { ["changedFiles"] = snap.FilesIndexed.ToString(), ["accessErrors"] = snap.AccessErrors.ToString(), ["mode"] = mode.ToString() });
        onProgress(snap);
        return (cachedRoot, new ScanOutcome(status, snap.AccessErrors));
    }

    private static void Refresh(DiskNode node, IncrementalMode mode, ProgressCounter counter, Action<Progress> onProgress, bool parallel, CancellationToken ct)
    {
        if (ct.IsCancellationRequested) return;

        if (mode == IncrementalMode.StructureOnly && node.FilesFingerprint != 0 && SafeMTime(node.Path) == node.DirectoryModifiedAtTicks)
        {
            foreach (var c in node.Children.Values.Where(c => c.IsDirectory).ToList())
                Refresh(c, mode, counter, onProgress, false, ct);
            SumChildren(node);
            return;
        }

        var listing = List(node.Path);
        if (listing is null) { counter.NoteError(); return; }   // inaccesibil acum: pastram subarborele din cache
        counter.NoteError(listing.Errors);
        node.DirectoryModifiedAtTicks = listing.DirMTimeTicks;

        if (Fingerprint(listing.Entries) != node.FilesFingerprint)
        {
            ApplyFiles(listing, node);
            ReportFiles(listing, counter, onProgress);
        }

        var liveDirs = listing.Entries.Where(e => e.IsDirectory).ToList();
        var liveNames = new HashSet<string>(liveDirs.Select(d => d.Name), StringComparer.OrdinalIgnoreCase);
        foreach (var name in node.Children.Where(kv => kv.Value.IsDirectory && !liveNames.Contains(kv.Key)).Select(kv => kv.Key).ToList())
            node.Children.Remove(name);

        var existing = new List<DiskNode>();
        var fresh = new List<DiskNode>();
        foreach (var d in liveDirs)
        {
            if (node.Children.TryGetValue(d.Name, out var c) && c.IsDirectory) existing.Add(c);
            else { node.Children.Remove(d.Name); fresh.Add(new DiskNode(d.Name, d.Path, isDirectory: true)); }
        }
        void Work(int i)
        {
            if (i < existing.Count) Refresh(existing[i], mode, counter, onProgress, false, ct);
            else Populate(fresh[i - existing.Count], counter, onProgress, false, ct);
        }
        var total = existing.Count + fresh.Count;
        if (parallel) Parallel.For(0, total, Work); else for (var i = 0; i < total; i++) Work(i);
        foreach (var n in fresh) node.Children[n.Name] = n;
        SumChildren(node);
    }

    // MARK: - Constructie / agregare

    private static void ApplyFiles(Listing listing, DiskNode node)
    {
        foreach (var k in node.Children.Where(kv => !kv.Value.IsDirectory).Select(kv => kv.Key).ToList()) node.Children.Remove(k);
        foreach (var e in listing.Entries.Where(e => !e.IsDirectory))
            node.Children[e.Name] = new DiskNode(e.Name, e.Path, isDirectory: false)
            { SizeBytes = e.Size, AllocatedBytes = e.Allocated, ModifiedAtTicks = e.MTimeTicks, IsCloudOnly = e.CloudOnly };
        node.FilesFingerprint = Fingerprint(listing.Entries);
    }

    private static void ReportFiles(Listing listing, ProgressCounter counter, Action<Progress> onProgress)
    {
        var files = listing.Entries.Where(e => !e.IsDirectory).ToList();
        if (files.Count > 0) counter.Add(files.Count, files.Sum(f => f.Size), onProgress);
    }

    private static void SumChildren(DiskNode node)
    {
        long s = 0, a = 0;
        foreach (var c in node.Children.Values) { s += c.SizeBytes; a += c.AllocatedBytes; }
        node.SizeBytes = s; node.AllocatedBytes = a;
    }

    /// FNV-1a 64 peste fisierele sortate ordinal dupa nume (nume, marime, alocat, mtime). Niciodata 0.
    private static long Fingerprint(List<Entry> entries)
    {
        ulong h = 0xcbf29ce484222325;
        void Mix(ulong v) { h = (h ^ v) * 0x100000001b3; }
        var files = entries.Where(e => !e.IsDirectory).OrderBy(e => e.Name.ToUpperInvariant(), StringComparer.Ordinal).ToList();
        foreach (var f in files)
        {
            foreach (var b in Encoding.UTF8.GetBytes(f.Name.ToUpperInvariant())) Mix(b);
            Mix(0xFF); Mix((ulong)f.Size); Mix((ulong)f.Allocated); Mix((ulong)f.MTimeTicks);
        }
        Mix((ulong)files.Count);
        var v = unchecked((long)h);
        return v == 0 ? 1 : v;
    }

    // MARK: - Stergere

    public static void Remove(string nodePath, DiskNode root)
    {
        var rootPrefix = root.Path.EndsWith(System.IO.Path.DirectorySeparatorChar) ? root.Path : root.Path + System.IO.Path.DirectorySeparatorChar;
        if (!nodePath.StartsWith(rootPrefix, StringComparison.OrdinalIgnoreCase)) return;
        var components = nodePath[rootPrefix.Length..].Split(System.IO.Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries);
        if (components.Length == 0) return;
        var chain = new List<DiskNode> { root };
        var current = root;
        foreach (var component in components)
        {
            if (!current.Children.TryGetValue(component, out var child)) return;
            chain.Add(child); current = child;
        }
        var size = current.SizeBytes; var alloc = current.AllocatedBytes;
        for (var i = 0; i < chain.Count - 1; i++) { chain[i].SizeBytes -= size; chain[i].AllocatedBytes -= alloc; }
        chain[^2].Children.Remove(components[^1]);
        chain[^2].FilesFingerprint = 0;   // la urmatoarea verificare folderul se recitește
    }

    // MARK: - Helpers native

    private static long SafeMTime(string path)
    {
        try { return Directory.Exists(path) ? Directory.GetLastWriteTimeUtc(path).Ticks : 0; } catch { return 0; }
    }

    /// Un nivel de director. null doar daca nu se poate deschide. Omite puncte de reparse (directoare) si
    /// fisierele "doar in cloud" sunt raportate cu alocat 0, fara a fi descarcate.
    private static Listing? List(string path)
    {
        try
        {
            var entries = new List<Entry>();
            int errors = 0;
            var dir = new DirectoryInfo(path);
            foreach (var fsi in dir.EnumerateFileSystemInfos())
            {
                try
                {
                    var attrs = fsi.Attributes;
                    if ((attrs & FileAttributes.Directory) != 0)
                    {
                        if ((attrs & FileAttributes.ReparsePoint) != 0) continue;   // junction/symlink: nu se urmareste
                        entries.Add(new Entry(fsi.Name, fsi.FullName, true, 0, 0, 0, false));
                    }
                    else
                    {
                        var fi = (FileInfo)fsi;
                        const FileAttributes Recall = (FileAttributes)0x00400000 | (FileAttributes)0x00040000;   // RECALL_ON_DATA_ACCESS | RECALL_ON_OPEN
                        var cloud = (attrs & Recall) != 0 || (attrs & FileAttributes.Offline) != 0;
                        long alloc = fi.Length;
                        if (cloud) alloc = 0;
                        else if ((attrs & (FileAttributes.SparseFile | FileAttributes.Compressed)) != 0) alloc = CompressedSize(fi.FullName, fi.Length);
                        entries.Add(new Entry(fsi.Name, fsi.FullName, false, fi.Length, alloc, fi.LastWriteTimeUtc.Ticks, cloud));
                    }
                }
                catch { errors++; }
            }
            return new Listing(entries, SafeMTime(path), errors);
        }
        catch { return null; }
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern uint GetCompressedFileSizeW(string lpFileName, out uint lpFileSizeHigh);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool GetVolumeInformationW(string root, StringBuilder? volName, int volNameLen, out uint serial,
        out uint maxComp, out uint flags, StringBuilder? fsName, int fsNameLen);

    private static long CompressedSize(string path, long fallback)
    {
        var low = GetCompressedFileSizeW(path, out var high);
        if (low == 0xFFFFFFFF && Marshal.GetLastWin32Error() != 0) return fallback;
        return ((long)high << 32) | low;
    }

    /// Serialul volumului (hex) sau null daca nu se poate determina (ex. cale UNC).
    public static string? VolumeSerial(string path)
    {
        try
        {
            var root = System.IO.Path.GetPathRoot(System.IO.Path.GetFullPath(path));
            if (string.IsNullOrEmpty(root)) return null;
            return GetVolumeInformationW(root, null, 0, out var serial, out _, out _, null, 0) ? serial.ToString("X8") : null;
        }
        catch { return null; }
    }
}
