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
        /// Elemente inaccesibile la ultima scanare (0 = complet).
        public int AccessErrors { get; set; }
        /// Ultima verificare la nivel de fisier; null = cache vechi, nereconciliat.
        public DateTime? VerifiedAtUtc { get; set; }
        /// Serialul volumului la momentul salvarii; alt volum la aceeasi litera invalideaza cache-ul.
        public string? VolumeSerial { get; set; }
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
            // Alt volum la aceeasi litera de unitate (disc extern inlocuit): cache-ul ar minti.
            var now = DiskScanEngine.VolumeSerial(rootPath);
            if (snapshot.VolumeSerial is not null && now is not null && snapshot.VolumeSerial != now)
            {
                DiagnosticLog.Log("warning", "disk", "cache.volume-mismatch");
                return null;
            }
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
            snapshot.VolumeSerial ??= DiskScanEngine.VolumeSerial(snapshot.RootPath);
            var target = FileFor(snapshot.RootPath);
            var tmp = target + ".tmp";
            File.WriteAllText(tmp, JsonSerializer.Serialize(snapshot));
            File.Move(tmp, target, overwrite: true);   // atomic: un crash nu lasa un cache pe jumatate scris
        }
        catch (Exception ex)
        {
            // nescrierea cache-ului nu trebuie sa blocheze UI-ul, dar se logheaza
            DiagnosticLog.Log("error", "disk", "cache.write-failed", ex.GetType().Name);
        }
    }

    public static void Clear(string rootPath)
    {
        try { File.Delete(FileFor(rootPath)); } catch { /* deja lipsa */ }
    }
}
