namespace MacMasterControlPro.Core.Services;

/// Log de diagnostic pe disc, la %TEMP%\mmcpro-crash.log — port 1:1 al
/// `DiagnosticLog.cs` (GDCPluginManagerWin), Regula 25. Singura sursa de
/// adevar reala cand un client raporteaza un bug fara sa avem acces live la
/// masina lui (vezi CLAUDE.md "Unde se ruleaza testele reale").
public static class DiagnosticLog
{
    private static readonly string Path_ = Path.Combine(Path.GetTempPath(), "mmcpro-crash.log");

    public static string FilePath => Path_;

    public static void Write(string tag, string message)
    {
        try { File.AppendAllText(Path_, $"[{DateTime.Now:HH:mm:ss.fff}] [{tag}] {message}\n"); }
        catch { /* best-effort */ }
    }

    /// Desface lanțul complet de `InnerException` — vezi nota din
    /// GDCPluginManagerWin/DiagnosticLog.cs pentru motivul real (un
    /// `ex.Message` simplu ascunde cauza reala a unui crash la pornire).
    public static string Describe(Exception ex)
    {
        var parts = new List<string>();
        for (var e = ex; e is not null; e = e.InnerException)
            parts.Add($"{e.GetType().Name}: {e.Message}\n{e.StackTrace}");
        return string.Join("\n<-\n", parts);
    }
}
