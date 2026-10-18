using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace MacMasterControlPro.Core.Services;

/// Jurnal de diagnostic local (OBSERVABILITY_STANDARD, Regulile 25/39): JSON pe linie,
/// rotativ (5 MB x fisier activ + 3 arhive) in %LOCALAPPDATA%\MacMasterControlPro\Logs,
/// redactat inainte de scriere, fara sa arunce si fara sa blocheze apelantul.
/// Pastreaza API-ul vechi `Write(tag, message)` / `Describe(ex)`.
public static class DiagnosticLog
{
    public const long MaxFileBytes = 5_000_000;
    public const int KeepArchives = 3;
    public static readonly string SessionId = Guid.NewGuid().ToString("N")[..8];
    public static string? DirectoryOverride { get; set; }
    public static string MinimumLevel { get; set; } = "info";
    private static readonly string[] Levels = { "trace", "debug", "info", "warning", "error", "critical" };
    private static readonly object Gate = new();

    public static string Directory_ => DirectoryOverride ?? Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MacMasterControlPro", "Logs");
    public static string FilePath => Path.Combine(Directory_, "mcsp.log");
    public static string AppVersion => typeof(DiagnosticLog).Assembly.GetName().Version?.ToString(3) ?? "dev";

    public static void Write(string tag, string message) => Log("info", tag, "msg", message);

    public static void Log(string level, string tag, string evt, string message = "", string? op = null,
        string? phase = null, long? ms = null, string? result = null, IDictionary<string, string>? fields = null)
    {
        try
        {
            if (Array.IndexOf(Levels, level) < Array.IndexOf(Levels, MinimumLevel)) return;
            var o = new SortedDictionary<string, object?>
            {
                ["ts"] = DateTimeOffset.Now.ToString("o"), ["lvl"] = level, ["sub"] = tag, ["evt"] = evt,
                ["ses"] = SessionId, ["ver"] = AppVersion, ["plat"] = "windows",
            };
            if (message.Length > 0) o["msg"] = Redact(message);
            if (op != null) o["op"] = op;
            if (phase != null) o["phase"] = phase;
            if (ms != null) o["ms"] = ms;
            if (result != null) o["res"] = result;
            if (fields != null) foreach (var kv in fields) o["f_" + kv.Key] = Redact(kv.Value);
            Append(JsonSerializer.Serialize(o) + "\n");
        }
        catch { /* eroarea loggerului nu afecteaza operatia principala */ }
    }

    private static void Append(string line)
    {
        lock (Gate)
        {
            System.IO.Directory.CreateDirectory(Directory_);
            var fi = new FileInfo(FilePath);
            if (fi.Exists && fi.Length + line.Length > MaxFileBytes) Rotate();
            File.AppendAllText(FilePath, line, Encoding.UTF8);
        }
    }

    private static void Rotate()
    {
        var b = FilePath;
        try { File.Delete($"{b}.{KeepArchives}"); } catch { }
        for (var i = KeepArchives - 1; i >= 1; i--)
            if (File.Exists($"{b}.{i}")) File.Move($"{b}.{i}", $"{b}.{i + 1}", true);
        File.Move(b, $"{b}.1", true);
    }

    private static readonly (Regex, string)[] Rules =
    {
        (new(@"(?i)(password|passwd|pass|secret|token|apikey|api_key|client_secret|authorization)([""']?\s*[:=]\s*[""']?)[^\s""',;&]+"), "$1$2<redacted>"),
        (new(@"(?i)bearer\s+[A-Za-z0-9._\-]+"), "Bearer <redacted>"),
        (new(@"[A-Za-z0-9._%+\-]+@[A-Za-z0-9.\-]+\.[A-Za-z]{2,}"), "<email>"),
        (new(@"[A-Za-z0-9_\-]{32,}"), "<opaque>"),
    };

    /// Redacteaza secrete, emailuri si numele de utilizator din cai.
    public static string Redact(string text)
    {
        var s = text;
        foreach (var (rx, rep) in Rules) s = rx.Replace(s, rep);
        s = Regex.Replace(s, @"(?i)([A-Z]:\\Users\\)[^\\\s""]+", "$1<user>");
        return s;
    }

    /// Operatie corelata: id, faze, durata, rezultat.
    public sealed class Operation
    {
        public string Id { get; } = Guid.NewGuid().ToString("N")[..8];
        private readonly string _tag, _name; private readonly Stopwatch _sw = Stopwatch.StartNew(); private int _ended;
        internal Operation(string tag, string name) { _tag = tag; _name = name; Log("info", tag, name + ".start", op: Id, phase: "start"); }
        public void Phase(string phase, string message = "") => Log("info", _tag, _name + ".phase", message, Id, phase, _sw.ElapsedMilliseconds);
        public void Progress(long items, long errors = 0) => Log("debug", _tag, _name + ".progress", op: Id, phase: "run", ms: _sw.ElapsedMilliseconds,
            fields: new Dictionary<string, string> { ["items"] = items.ToString(), ["errors"] = errors.ToString() });
        public void End(string result, string level = "info", string message = "", IDictionary<string, string>? fields = null)
        {
            if (Interlocked.Exchange(ref _ended, 1) == 1) return;
            Log(level, _tag, _name + ".end", message, Id, "end", _sw.ElapsedMilliseconds, result, fields);
        }
    }
    public static Operation Begin(string tag, string name) => new(tag, name);

    /// Desface lantul complet de `InnerException` (ex.Message simplu ascunde cauza reala).
    public static string Describe(Exception ex)
    {
        var parts = new List<string>();
        for (var e = ex; e is not null; e = e.InnerException)
            parts.Add($"{e.GetType().Name}: {e.Message}\n{e.StackTrace}");
        return string.Join("\n<-\n", parts);
    }
}
