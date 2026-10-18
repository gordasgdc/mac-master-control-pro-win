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

    public static string Explanation(string path, bool isDirectory, ISet<SpaceFlag> flags, string lang = "ro") => Localized(ExplanationRo(path, isDirectory, flags), lang);

    private static readonly Dictionary<string, (string En, string Es)> Translations = new()
    {
        ["Adobe recreează cache-ul media la nevoie."] = ("Adobe recreates its media cache when needed.", "Adobe recrea su caché de medios cuando hace falta."),
        ["Aplicație."] = ("Application.", "Aplicación."),
        ["Aplicație: se dezinstalează din Dezinstalator, cu toate urmele."] = ("Application: remove it with the Uninstaller, leftovers included.", "Aplicación: se desinstala con el Desinstalador, con todos sus restos."),
        ["Aplicații instalate: se dezinstalează din Dezinstalator."] = ("Installed applications: remove them with the Uninstaller.", "Aplicaciones instaladas: se desinstalan con el Desinstalador."),
        ["Backup de dispozitiv iOS: nu se șterge de aici."] = ("iOS device backup: do not delete it from here.", "Copia de seguridad de un dispositivo iOS: no se borra desde aquí."),
        ["Baza de date DaVinci Resolve: conține proiectele."] = ("DaVinci Resolve database: it holds your projects.", "Base de datos de DaVinci Resolve: contiene tus proyectos."),
        ["Bibliotecă Foto: se gestionează din aplicația Foto."] = ("Photos library: manage it from the Photos app.", "Fototeca: se gestiona desde la app Fotos."),
        ["Cache de aplicații: se recreează la nevoie."] = ("App caches: recreated when needed.", "Cachés de apps: se recrean cuando hace falta."),
        ["Chei de protecție."] = ("Protection keys.", "Claves de protección."),
        ["Chei și parole."] = ("Keys and passwords.", "Claves y contraseñas."),
        ["Coșul de reciclare se golește din Windows."] = ("The Recycle Bin is emptied from Windows.", "La Papelera de reciclaje se vacía desde Windows."),
        ["Credențiale."] = ("Credentials.", "Credenciales."),
        ["DaVinci Resolve recreează CacheClip la nevoie."] = ("DaVinci Resolve recreates CacheClip when needed.", "DaVinci Resolve recrea CacheClip cuando hace falta."),
        ["Date Mail."] = ("Mail data.", "Datos de Mail."),
        ["Date Mesaje."] = ("Messages data.", "Datos de Mensajes."),
        ["Date de sistem."] = ("System data.", "Datos del sistema."),
        ["Datele derivate ale build-ului se recreează."] = ("Build-derived data is recreated.", "Los datos derivados de la compilación se recrean."),
        ["Fișiere temporare: aplicațiile le recreează."] = ("Temporary files: apps recreate them.", "Archivos temporales: las apps los recrean."),
        ["Fișierele de formă de undă se regenerează."] = ("Waveform files are regenerated.", "Los archivos de forma de onda se regeneran."),
        ["Folder personal: selectează conținutul, nu folderul întreg."] = ("Personal folder: select its contents, not the whole folder.", "Carpeta personal: selecciona su contenido, no la carpeta entera."),
        ["Folder sincronizat în cloud: ștergerea se propagă; se gestionează din OneDrive."] = ("Cloud-synced folder: deleting propagates; manage it from OneDrive.", "Carpeta sincronizada en la nube: el borrado se propaga; se gestiona desde OneDrive."),
        ["Media optimizată se regenerează din originale."] = ("Optimized media is regenerated from the originals.", "Los medios optimizados se regeneran a partir de los originales."),
        ["Sistem."] = ("System.", "Sistema."),
        ["Time Machine."] = ("Time Machine.", "Time Machine."),
        ["Xcode recreează DerivedData la build."] = ("Xcode recreates DerivedData on build.", "Xcode recrea DerivedData al compilar."),
        ["iCloud Drive: se gestionează din iCloud."] = ("iCloud Drive: manage it from iCloud.", "iCloud Drive: se gestiona desde iCloud."),
        ["Element fără cale reală pe disc (grup de fișiere mici)."] = ("Item with no real path on disk (group of small files).", "Elemento sin ruta real en el disco (grupo de archivos pequeños)."),
        ["Conținut identic cu o altă copie (sumă de control confirmată). Se păstrează o copie."] = ("Content identical to another copy (checksum confirmed). One copy is kept.", "Contenido idéntico a otra copia (suma de control confirmada). Se conserva una copia."),
        ["Nicio regulă verificată nu se aplică: nu știm dacă există o copie de rezervă. Nu intră în totalul recuperabil; îl poți muta în Coș doar cu confirmare."] = ("No verified rule applies: we do not know whether a backup exists. It does not count toward the recoverable total; you can move it to the Trash only with confirmation.", "No se aplica ninguna regla verificada: no sabemos si existe una copia de seguridad. No cuenta en el total recuperable; solo puedes moverlo a la Papelera con confirmación."),
    };
    /// Traduce explicatia (cheia = textul romanesc din reguli); necunoscut => textul romanesc.
    public static string Localized(string reason, string lang) => Translations.TryGetValue(reason, out var t) ? (lang == "en" ? t.En : lang == "es" ? t.Es : reason) : reason;

    private static string ExplanationRo(string path, bool isDirectory, ISet<SpaceFlag> flags)
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
