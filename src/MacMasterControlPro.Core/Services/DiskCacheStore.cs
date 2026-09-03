using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace MacMasterControlPro.Core.Services;

/// Port 1:1 al DiskCacheStore.swift (Mac, v2.30.0) — persistă/încarcă
/// instant ultima analiză de disc completă, ca redeschiderea aplicației
/// să nu mai reia scanarea de la zero. Un fișier de cache PER rădăcină
/// scanată (`C:\`, `D:\` etc.).
///
/// Format: JSON simplu (`System.Text.Json`, deja folosit în acest repo —
/// `BigFileScanFolders`), nu un format binar dedicat — spre deosebire de
/// Mac (`PropertyListEncoder`), .NET nu are un echivalent binar la fel de
/// simplu, fără dependință nouă; JSON e suficient de rapid pentru un
/// arbore de câteva sute de mii de noduri.
public static class DiskCacheStore
{
    public sealed class Snapshot
    {
        public string RootPath { get; set; } = "";
        public DateTime ScannedAtUtc { get; set; }
        public DiskNode Root { get; set; } = new();
    }

    private static string CacheDirectory
    {
        get
        {
            var dir = System.IO.Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "MacMasterControlPro", "DiskCache");
            Directory.CreateDirectory(dir);
            return dir;
        }
    }

    /// Nume de fișier stabil, derivat prin SHA256 din calea rădăcinii —
    /// hash-uită în minuscule ÎNTÂI (`ToLowerInvariant`), fiindcă `C:\Date`
    /// și `c:\date` sunt ACEEAȘI cale pe Windows și trebuie să rezolve la
    /// același fișier de cache.
    private static string FileFor(string rootPath)
    {
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(rootPath.ToLowerInvariant())))[..24];
        return System.IO.Path.Combine(CacheDirectory, $"{hash}.json");
    }

    public static Snapshot? Load(string rootPath)
    {
        try
        {
            var file = FileFor(rootPath);
            if (!File.Exists(file)) return null;
            var snapshot = JsonSerializer.Deserialize<Snapshot>(File.ReadAllText(file));
            if (snapshot is null) return null;
            // Un cache cu alta radacina (coliziune de hash, practic
            // imposibila cu SHA-256, dar verificam explicit — Regula 30,
            // nu presupunem) nu se foloseste niciodata orb.
            if (!string.Equals(snapshot.RootPath, rootPath, StringComparison.OrdinalIgnoreCase)) return null;
            return snapshot;
        }
        catch
        {
            return null; // cache corupt/lipsa - cade pe scanare completa
        }
    }

    public static void Save(Snapshot snapshot)
    {
        try
        {
            File.WriteAllText(FileFor(snapshot.RootPath), JsonSerializer.Serialize(snapshot));
        }
        catch
        {
            // nescrierea cache-ului nu trebuie sa blocheze UI-ul
        }
    }

    public static void Clear(string rootPath)
    {
        try { File.Delete(FileFor(rootPath)); } catch { /* deja lipsa */ }
    }
}
