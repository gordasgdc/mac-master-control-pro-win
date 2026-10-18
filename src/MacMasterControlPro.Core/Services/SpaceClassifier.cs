namespace MacMasterControlPro.Core.Services;

/// Stări de siguranță ale unui element din analiza discului. UI le afișează cu text + pictogramă, nu doar culoare.
public enum SpaceFlag { Regenerable, Duplicate, Protected }

/// Reguli explicite și explicabile. Nimic nu e „regenerabil” fără o regulă din această listă.
public static class SpaceClassifier
{
    public sealed record Rule(string Id, string Reason);

    private static readonly Dictionary<string, Rule> RegenerableNames = new(StringComparer.OrdinalIgnoreCase)
    {
        ["cacheclip"] = new("resolve-cacheclip", "DaVinci Resolve recreează CacheClip la nevoie."),
        ["cache clip"] = new("resolve-cacheclip", "DaVinci Resolve recreează CacheClip la nevoie."),
        ["optimized media"] = new("resolve-optimized", "Media optimizată se regenerează din originale."),
        ["optimizedmedia"] = new("resolve-optimized", "Media optimizată se regenerează din originale."),
        ["adobe media cache"] = new("adobe-media-cache", "Adobe recreează cache-ul media la nevoie."),
        ["media cache files"] = new("adobe-media-cache", "Adobe recreează cache-ul media la nevoie."),
        ["peak files"] = new("adobe-peak", "Fișierele de formă de undă se regenerează."),
        ["deriveddata"] = new("derived-data", "Datele derivate ale build-ului se recreează."),
    };

    private static readonly (string Fragment, Rule Rule)[] ProtectedFragments =
    {
        (@"\MobileSync\Backup", new("ios-backup", "Backup de dispozitiv iOS: nu se șterge de aici.")),
        (@"\$Recycle.Bin", new("recycle", "Coșul de reciclare se golește din Windows.")),
        (@"\System Volume Information", new("system", "Sistem.")),
        (@"\Windows\", new("system", "Sistem.")),
        (@"\Program Files", new("programs", "Aplicații instalate: se dezinstalează din Dezinstalator.")),
        (@"\ProgramData\Microsoft", new("system", "Date de sistem.")),
        (@"\AppData\Roaming\Microsoft\Credentials", new("credentials", "Credențiale.")),
        (@"\AppData\Roaming\Microsoft\Protect", new("credentials", "Chei de protecție.")),
        (@"\OneDrive", new("onedrive", "Folder sincronizat în cloud: ștergerea se propagă; se gestionează din OneDrive.")),
        (@"\Resolve Disk Database", new("resolve-db", "Baza de date DaVinci Resolve: conține proiectele.")),
    };

    private static readonly string[] UserFolders = { "Documents", "Desktop", "Pictures", "Music", "Videos", "Downloads", "AppData" };

    public static HashSet<SpaceFlag> Flags(string path, bool isDirectory, bool isAggregate = false, ISet<string>? duplicateSurplus = null)
    {
        var f = new HashSet<SpaceFlag>();
        if (isAggregate) { f.Add(SpaceFlag.Protected); return f; }
        if (ProtectedReason(path, isDirectory) is not null) f.Add(SpaceFlag.Protected);
        if (isDirectory && RegenerableRule(path) is not null) f.Add(SpaceFlag.Regenerable);
        if (duplicateSurplus?.Contains(path) == true) f.Add(SpaceFlag.Duplicate);
        if (f.Contains(SpaceFlag.Protected)) { f.Remove(SpaceFlag.Regenerable); f.Remove(SpaceFlag.Duplicate); }
        return f;
    }

    public static Rule? RegenerableRule(string path)
    {
        var name = Path.GetFileName(path.TrimEnd('\\'));
        if (RegenerableNames.TryGetValue(name, out var r)) return r;
        if (path.EndsWith(@"\AppData\Local\Temp", StringComparison.OrdinalIgnoreCase)) return new("user-temp", "Fișiere temporare: aplicațiile le recreează.");
        return null;
    }

    public static Rule? ProtectedReason(string path, bool isDirectory)
    {
        foreach (var (frag, rule) in ProtectedFragments) if (path.Contains(frag, StringComparison.OrdinalIgnoreCase)) return rule;
        if (path.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) && path.Contains(@"\Program Files", StringComparison.OrdinalIgnoreCase)) return new("programs", "Aplicație.");
        // foldere personale întregi: C:\Users\<nume>\Documents etc. (conținutul lor poate fi selectat)
        var parts = path.Split('\\', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 4 && parts[1].Equals("Users", StringComparison.OrdinalIgnoreCase) && UserFolders.Contains(parts[3], StringComparer.OrdinalIgnoreCase))
            return new("user-folder", "Folder personal: selectează conținutul, nu folderul întreg.");
        return null;
    }

    public static string Explanation(string path, bool isDirectory, ISet<SpaceFlag> flags)
    {
        if (flags.Contains(SpaceFlag.Protected)) return ProtectedReason(path, isDirectory)?.Reason ?? "Element fără cale reală pe disc (grup de fișiere mici).";
        if (flags.Contains(SpaceFlag.Regenerable) && RegenerableRule(path) is { } r) return r.Reason;
        if (flags.Contains(SpaceFlag.Duplicate)) return "Conținut identic cu o altă copie (sumă de control confirmată). Se păstrează o copie.";
        return "Nicio regulă verificată nu se aplică: nu știm dacă există o copie de rezervă. Nu intră în totalul recuperabil; îl poți muta în Coș doar cu confirmare.";
    }
}

public sealed class SpaceSelectionSummary
{
    public long RecoverableBytes, UnverifiedBytes;
    public List<string> Recoverable = new(), Unverified = new(), Blocked = new();
    public long MoveBytes => RecoverableBytes + UnverifiedBytes;
}

/// Selecția și totalurile, fără dublă numărare.
public static class SpaceMath
{
    public static SpaceSelectionSummary Summarize(IEnumerable<DiskNode> nodes, ISet<string>? duplicateSurplus = null)
    {
        var list = nodes.ToList();
        var paths = new HashSet<string>(list.Select(n => n.Path), StringComparer.OrdinalIgnoreCase);
        bool HasSelectedAncestor(string p)
        {
            var cur = Path.GetDirectoryName(p);
            while (!string.IsNullOrEmpty(cur)) { if (paths.Contains(cur)) return true; cur = Path.GetDirectoryName(cur); }
            return false;
        }
        var s = new SpaceSelectionSummary();
        foreach (var n in list)
        {
            if (HasSelectedAncestor(n.Path)) continue;
            var f = SpaceClassifier.Flags(n.Path, n.IsDirectory, false, duplicateSurplus);
            if (f.Contains(SpaceFlag.Protected)) { s.Blocked.Add(n.Path); continue; }
            if (f.Contains(SpaceFlag.Regenerable) || f.Contains(SpaceFlag.Duplicate)) { s.RecoverableBytes += n.SizeBytes; s.Recoverable.Add(n.Path); }
            else { s.UnverifiedBytes += n.SizeBytes; s.Unverified.Add(n.Path); }
        }
        return s;
    }

    public static (long Regenerable, long DuplicateSurplus, long Overlap, long Recoverable) VolumeTotals(DiskNode root, ISet<string> duplicateSurplus)
    {
        long reg = 0, dup = 0, overlap = 0;
        void Walk(DiskNode n, bool insideReg)
        {
            var f = SpaceClassifier.Flags(n.Path, n.IsDirectory, false, duplicateSurplus);
            if (f.Contains(SpaceFlag.Protected)) return;
            var inReg = insideReg;
            if (f.Contains(SpaceFlag.Regenerable) && !insideReg) { reg += n.SizeBytes; inReg = true; }
            if (f.Contains(SpaceFlag.Duplicate)) { if (insideReg) overlap += n.SizeBytes; else dup += n.SizeBytes; }
            foreach (var c in n.Children.Values) Walk(c, inReg);
        }
        Walk(root, false);
        return (reg, dup, overlap, reg + dup);
    }
}
