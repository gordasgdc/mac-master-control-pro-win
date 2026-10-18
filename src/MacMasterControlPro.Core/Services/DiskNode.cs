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
    /// Mărimea LOGICĂ (Length); suma pe subarbore la foldere.
    public long SizeBytes { get; set; }

    /// Spațiul ocupat PE DISC (alocat, raportat de sistem): măsura din „ce ocupă spațiul” (hartă, listă, procente, totaluri). Diferă de `SizeBytes` la fișiere sparse/comprimate/doar-în-cloud. NU garantează spațiu recuperat: hard link-urile, clonele ReFS și instantaneele pot ține aceleași blocuri.
    public long UsedBytes => AllocatedBytes;

    /// Doar pentru foldere — 0 pentru fișiere (nefolosit acolo). Mtime-ul
    /// (UTC, `.Ticks`) folderului la ultima scanare — cheia scanării
    /// incrementale (`DiskScanEngine.RefreshDirectory`): un folder al cărui
    /// mtime nu s-a schimbat de la ultima scanare nu are nevoie să fie
    /// recitit de pe disc.
    public long DirectoryModifiedAtTicks { get; set; }

    /// Spatiu alocat pe disc (fisiere sparse/comprimate/doar-in-cloud ocupa mai putin decat dimensiunea logica).
    /// 0 pentru cache-uri vechi pana la prima verificare.
    public long AllocatedBytes { get; set; }

    /// Doar fisiere: LastWriteTimeUtc.Ticks.
    public long ModifiedAtTicks { get; set; }

    /// Doar foldere: amprenta (nume, marime, alocat, mtime) a fisierelor DIRECTE. 0 = necunoscuta.
    public long FilesFingerprint { get; set; }

    /// Fisier "doar in cloud" (OneDrive etc.): nu se descarca niciodata pentru analiza.
    public bool IsCloudOnly { get; set; }

    public Dictionary<string, DiskNode> Children { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    public DiskNode() { }

    public DiskNode(string name, string path, bool isDirectory)
    {
        Name = name;
        Path = path;
        IsDirectory = isDirectory;
    }

    [JsonIgnore]
    public IEnumerable<DiskNode> SortedChildren => Children.Values.OrderByDescending(c => c.UsedBytes);

    [JsonIgnore]
    public int TotalFileCount => IsDirectory ? Children.Values.Sum(c => c.TotalFileCount) : 1;

    [JsonIgnore]
    public string SizeDescription => FormatBytes(UsedBytes);

    public static string FormatBytes(long bytes)
    {
        double b = bytes;
        string[] units = { "B", "KB", "MB", "GB", "TB" };
        int i = 0;
        while (b >= 1024 && i < units.Length - 1) { b /= 1024; i++; }
        return $"{b:0.#} {units[i]}";
    }
}
