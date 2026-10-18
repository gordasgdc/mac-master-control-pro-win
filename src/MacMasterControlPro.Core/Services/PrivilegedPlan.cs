namespace MacMasterControlPro.Core.Services;

/// Plan de operații privilegiate: toți pașii unei acțiuni (sau ai unei selecții) se prezintă împreună, se autorizează O SINGURĂ dată
/// (un singur proces PowerShell elevat, adică un singur prompt UAC) și se execută într-un singur proces. Anularea oprește întregul plan:
/// nu urmează alte ferestre. Parola nu este văzută, salvată sau transmisă; nu se dezactivează UAC și nu rămâne niciun proces elevat după plan.
public static class PrivilegedPlan
{
    public sealed record Step(string Label, string Command);
    public sealed record Outcome(bool Cancelled, IReadOnlyDictionary<string, string?> Results);

    /// Prezintă planul înainte de promptul UAC (setat de aplicație). Fără aprobator, planul se refuză.
    public static Func<IReadOnlyList<string>, string, bool>? Approver { get; set; }

    public static string Q(string s) => "'" + s.Replace("'", "''") + "'";

    /// Căi pe care o ștergere privilegiată nu le atinge niciodată (rădăcini, directoare de sistem, profilul utilizatorului).
    public static bool IsSafeDeletePath(string path)
    {
        try
        {
            if (!Path.IsPathRooted(path) || path.Contains('\n') || path.Contains('\0')) return false;
            var full = Path.GetFullPath(path).TrimEnd('\\');
            var root = Path.GetPathRoot(full)?.TrimEnd('\\') ?? "";
            if (full.Equals(root, StringComparison.OrdinalIgnoreCase)) return false;
            var blocked = new[] { Environment.GetFolderPath(Environment.SpecialFolder.Windows), Environment.GetFolderPath(Environment.SpecialFolder.System),
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
                Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                Path.Combine(root + "\\", "Users") };
            return !blocked.Any(b => !string.IsNullOrEmpty(b) && full.Equals(b.TrimEnd('\\'), StringComparison.OrdinalIgnoreCase));
        }
        catch { return false; }
    }

    public static Outcome Execute(string title, IReadOnlyList<Step> steps)
    {
        if (steps.Count == 0) return new Outcome(false, new Dictionary<string, string?>());
        var labels = steps.Select(s => s.Label).ToList();
        if (Approver is not { } approve || !approve(labels, title))
        {
            DiagnosticLog.Log("info", "privilege", "plan.declined", steps.Count.ToString());
            return new Outcome(true, labels.Distinct().ToDictionary(l => l, _ => (string?)"cancelled"));
        }
        // Un singur script, un singur prompt UAC. Fiecare pas își raportează singur rezultatul; un pas eșuat nu declanșează o nouă cerere.
        var script = string.Join("; ", steps.Select((s, i) => $"try {{ {s.Command}; Write-Output 'MMCP_STEP{i}:OK' }} catch {{ Write-Output 'MMCP_STEP{i}:FAIL' }}"));
        var r = PrivilegedRunner.RunDetailed(script, title);
        var results = new Dictionary<string, string?>();
        if (r.Cancelled || (!r.Completed && !r.Lines.Any(l => l.StartsWith("MMCP_STEP"))))
        {
            DiagnosticLog.Log("info", "privilege", "plan.cancelled", steps.Count.ToString());
            foreach (var l in labels) results[l] = r.Cancelled ? "cancelled" : "failed";
            return new Outcome(r.Cancelled, results);
        }
        for (var i = 0; i < steps.Count; i++)
            results[steps[i].Label] = r.Lines.Contains($"MMCP_STEP{i}:OK") ? null : CoreL.X("pasul a eșuat chiar cu drepturi de administrator", "the step failed even with administrator rights", "el paso falló incluso con derechos de administrador");
        return new Outcome(false, results);
    }
}
