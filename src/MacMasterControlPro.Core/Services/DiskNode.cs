using System.Text.Json.Serialization;

namespace MacMasterControlPro.Core.Services;

/// Port 1:1 al DiskTreeNode.swift (Mac, v2.30.0) — un nod din arborele de
/// fișiere/foldere indexat complet. Foldere: `SizeBytes` e SUMA tuturor
/// descendenților; fișiere: `SizeBytes` e mărimea proprie, `Children`
/// mereu gol.
///
/// `Children` folosește `StringComparer.OrdinalIgnoreCase` — spre
/// deosebire de Mac (APFS case-preserving, dar dicționarul Swift e
/// case-SENSITIVE), sistemul de fișiere Windows compară numele
/// case-INSENSITIV — fără asta, o rescanare incrementală ar putea crea
/// un nod DUPLICAT pentru același folder dacă numele lui ajunge listat
/// cu altă capitalizare între două scanări (rar, dar posibil pe unele
/// unități de rețea).
///
/// Constructor fără parametri + proprietăți publice, cerut de
/// `System.Text.Json` pentru (de)serializare — vezi `DiskCacheStore`.
public sealed class DiskNode
{
    public string Name { get; set; } = "";
    public string Path { get; set; } = "";
    public bool IsDirectory { get; set; }
    public long SizeBytes { get; set; }

    /// Doar pentru foldere — 0 pentru fișiere (nefolosit acolo). Mtime-ul
    /// (UTC, `.Ticks`) folderului la ultima scanare — cheia scanării
    /// incrementale (`DiskScanEngine.RefreshDirectory`): un folder al cărui
    /// mtime nu s-a schimbat de la ultima scanare nu are nevoie să fie
    /// recitit de pe disc.
    public long DirectoryModifiedAtTicks { get; set; }

    public Dictionary<string, DiskNode> Children { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    public DiskNode() { }

    public DiskNode(string name, string path, bool isDirectory)
    {
        Name = name;
        Path = path;
        IsDirectory = isDirectory;
    }

    [JsonIgnore]
    public IEnumerable<DiskNode> SortedChildren => Children.Values.OrderByDescending(c => c.SizeBytes);

    [JsonIgnore]
    public int TotalFileCount => IsDirectory ? Children.Values.Sum(c => c.TotalFileCount) : 1;

    [JsonIgnore]
    public string SizeDescription => FormatBytes(SizeBytes);

    public static string FormatBytes(long bytes)
    {
        double b = bytes;
        string[] units = { "B", "KB", "MB", "GB", "TB" };
        int i = 0;
        while (b >= 1024 && i < units.Length - 1) { b /= 1024; i++; }
        return $"{b:0.#} {units[i]}";
    }
}
