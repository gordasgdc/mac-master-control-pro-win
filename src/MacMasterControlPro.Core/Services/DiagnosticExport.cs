using System.IO.Compression;
using System.Text.Json;

namespace MacMasterControlPro.Core.Services;

/// Pachet de diagnostic local: ZIP redactat, creat doar la cererea utilizatorului; nu se incarca nimic.
public static class DiagnosticExport
{
    public sealed record Item(string Name, long Bytes, string Note);

    public static IReadOnlyList<Item> Preview()
    {
        var items = new List<Item> { new("manifest.json", Manifest().Length, "versiune, platforma, sesiune, ora") };
        foreach (var f in LogFiles())
            items.Add(new Item("logs/" + Path.GetFileName(f), new FileInfo(f).Length, "redactat (parole, emailuri, nume de utilizator din cai)"));
        return items;
    }

    public static string Export(string destinationFolder)
    {
        var name = $"MasterControlStudioPro-diagnostic-{DiagnosticLog.AppVersion}-{DateTime.Now:yyyyMMdd-HHmmss}.zip";
        var zipPath = Path.Combine(destinationFolder, name);
        using (var zip = ZipFile.Open(zipPath, ZipArchiveMode.Create))
        {
            void Add(string entry, string content)
            {
                using var w = new StreamWriter(zip.CreateEntry(entry).Open());
                w.Write(content);
            }
            Add("manifest.json", Manifest());
            foreach (var f in LogFiles())
            {
                string raw; try { using var fs = new FileStream(f, FileMode.Open, FileAccess.Read, FileShare.ReadWrite); using var r = new StreamReader(fs); raw = r.ReadToEnd(); } catch { continue; }
                Add("logs/" + Path.GetFileName(f), DiagnosticLog.Redact(raw));
            }
        }
        DiagnosticLog.Log("info", "diag", "export.done", result: "ok");
        return zipPath;
    }

    private static IEnumerable<string> LogFiles() =>
        Directory.Exists(DiagnosticLog.Directory_)
            ? Directory.GetFiles(DiagnosticLog.Directory_, "mcsp.log*").OrderBy(x => x)
            : Enumerable.Empty<string>();

    private static string Manifest() => JsonSerializer.Serialize(new Dictionary<string, string>
    {
        ["app"] = "Master Control Studio Pro", ["version"] = DiagnosticLog.AppVersion, ["platform"] = "windows",
        ["os"] = Environment.OSVersion.ToString(), ["session"] = DiagnosticLog.SessionId,
        ["generated"] = DateTimeOffset.Now.ToString("o"),
        ["contents"] = "log-uri redactate; fara continutul fisierelor, fara chei de licenta, fara parole",
    }, new JsonSerializerOptions { WriteIndented = true });
}
