using Microsoft.Win32;

namespace MacMasterControlPro.Core.Services;

/// Oglinda UninstallerService.swift (Mac) — scaneaza Registry (Uninstall
/// keys, sursa oficiala Windows a listei de aplicatii instalate) + toate
/// locatiile standard unde o aplicatie isi lasa urme.
public sealed record InstalledAppWin(string DisplayName, string? UninstallString, string? InstallLocation, string? QuietUninstallString = null);

public sealed record UninstallCategoryWin(string Id, string Title, List<string> Paths, long TotalBytes, bool RequiresPrivilege)
{
    public string SizeDescription => FormatBytes(TotalBytes);
    private static string FormatBytes(long bytes)
    {
        double b = bytes;
        string[] units = { "B", "KB", "MB", "GB" };
        int i = 0;
        while (b >= 1024 && i < units.Length - 1) { b /= 1024; i++; }
        return $"{b:0.#} {units[i]}";
    }
}

public static class UninstallerService
{
    private static readonly string[] UninstallRegistryKeys =
    {
        @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall",
        @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall",
    };

    /// [2026-09-03] FIX REAL, raportat de Cristi: aplicații ștergeau
    /// "cu succes" (dezinstalatorul oficial rula, exit code 0), dar tot
    /// apăreau în listă la re-scanare. O cauză reală, găsită direct în
    /// cod: se citea DOAR `Registry.LocalMachine` — multe aplicații (orice
    /// instalate per-utilizator, fără admin: Chrome, Discord, VS Code,
    /// Slack etc.) se înregistrează sub `HKEY_CURRENT_USER`, niciodată sub
    /// HKLM — lipseau complet din scanare pe acea cale, SAU (mai frecvent)
    /// aveau o intrare Registry DUBLĂ (HKLM + HKCU pentru aceeași
    /// aplicație) — ștergeai una, cealaltă rămânea, dând impresia că
    /// "nu s-a șters nimic".
    private static IEnumerable<(RegistryKey Hive, string Path)> AllUninstallRoots()
    {
        foreach (var path in UninstallRegistryKeys)
        {
            yield return (Registry.LocalMachine, path);
            yield return (Registry.CurrentUser, path);
        }
    }

    public static List<InstalledAppWin> ScanInstalledApps()
    {
        var results = new List<InstalledAppWin>();
        foreach (var (hive, keyPath) in AllUninstallRoots())
        {
            using var key = hive.OpenSubKey(keyPath);
            if (key is null) continue;
            foreach (var subName in key.GetSubKeyNames())
            {
                using var sub = key.OpenSubKey(subName);
                var name = sub?.GetValue("DisplayName") as string;
                if (string.IsNullOrWhiteSpace(name)) continue;
                results.Add(new InstalledAppWin(
                    name,
                    sub?.GetValue("UninstallString") as string,
                    sub?.GetValue("InstallLocation") as string,
                    sub?.GetValue("QuietUninstallString") as string));
            }
        }
        return results.DistinctBy(a => a.DisplayName)
            .OrderBy(a => a.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    /// Verificare REALĂ post-dezinstalare — nu ne mai bazăm doar pe codul
    /// de ieșire al dezinstalatorului (mulți dezinstalatori de tip
    /// wizard/NSIS se auto-relansează dintr-o copie temporară și procesul
    /// original iese aproape instant, MULT înainte ca userul să apuce să
    /// dea click prin fereastra reală) — verificăm dacă intrarea chiar a
    /// dispărut din Registry.
    public static bool IsStillRegistered(string displayName) =>
        ScanInstalledApps().Any(a => string.Equals(a.DisplayName, displayName, StringComparison.OrdinalIgnoreCase));

    /// [2026-09-03] Cerinta directa a lui Cristi (port 1:1 al fix-ului de pe
    /// Mac): "trebuie sa poata sa-l stearga chiar daca ruleaza... sa-l oprim
    /// chiar fortat". Un fisier executabil deschis (chiar dacă aplicația
    /// mamă e altceva - un helper de fundal e la fel de blocant) nu poate fi
    /// șters de dezinstalatorul oficial nici de ștergerea manuală a
    /// resturilor - Windows raporteaza "File in use"/acces refuzat. Inchide
    /// FORTAT (`Kill()`, echivalent SIGKILL) orice proces al carui
    /// executabil traieste sub `installLocation`, indiferent de nume -
    /// acopera si helper-e/servicii de fundal ale aceleiasi aplicatii, nu
    /// doar procesul principal.
    public static void TerminateIfRunning(string? installLocation)
    {
        if (string.IsNullOrWhiteSpace(installLocation)) return;
        string normalizedRoot;
        try { normalizedRoot = Path.GetFullPath(installLocation).TrimEnd('\\') + "\\"; }
        catch { return; }

        foreach (var process in System.Diagnostics.Process.GetProcesses())
        {
            try
            {
                var path = process.MainModule?.FileName;
                if (path is null) continue;
                if (!Path.GetFullPath(path).StartsWith(normalizedRoot, StringComparison.OrdinalIgnoreCase)) continue;
                process.Kill(entireProcessTree: true);
                process.WaitForExit(2000);
            }
            catch
            {
                // Proces de sistem fara acces la MainModule, deja iesit
                // intre timp, sau Kill() a esuat - continuam cu urmatorul,
                // nu blocam toata dezinstalarea pe un singur proces problematic.
            }
        }
    }

    /// Locatiile standard unde Windows lasa urme dupa o dezinstalare
    /// normala — %APPDATA%/%LOCALAPPDATA%/%PROGRAMDATA%, Registry HKCU,
    /// Scheduled Tasks. Cerinta directa (2026-08-31): "sa nu ramana nimica
    /// pe niciunde".
    public static List<UninstallCategoryWin> ScanRelatedFiles(InstalledAppWin app)
    {
        var name = app.DisplayName;
        var categories = new List<UninstallCategoryWin>();

        var roots = new (string id, string title, string root)[]
        {
            ("appdata", "AppData\\Roaming", Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData)),
            ("localappdata", "AppData\\Local", Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData)),
            ("programdata", "ProgramData", Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData)),
        };

        foreach (var (id, title, root) in roots)
        {
            if (!Directory.Exists(root)) continue;
            var matches = Directory.GetDirectories(root)
                .Where(d => Path.GetFileName(d).Contains(name, StringComparison.OrdinalIgnoreCase))
                .ToList();
            if (matches.Count == 0) continue;
            var total = matches.Sum(DirectorySize);
            categories.Add(new UninstallCategoryWin(id, title, matches, total, RequiresPrivilege: false));
        }

        // Registry — cerinta (2026-09-01): "sa elimine tot tot tot, sa
        // scaneze resturi". Pana acum doar HKCU\Software era verificat;
        // multe aplicatii (mai ales cele instalate pentru toti userii)
        // scriu si in HKLM (32-bit + 64-bit).
        var registryRoots = new (Microsoft.Win32.RegistryKey hive, string hiveLabel, string subPath)[]
        {
            (Registry.CurrentUser, "HKCU", @"Software"),
            (Registry.LocalMachine, "HKLM", @"Software"),
            (Registry.LocalMachine, "HKLM", @"Software\WOW6432Node"),
        };
        foreach (var (hive, hiveLabel, subPath) in registryRoots)
        {
            using var root = hive.OpenSubKey(subPath);
            var match = root?.GetSubKeyNames().FirstOrDefault(n => n.Contains(name, StringComparison.OrdinalIgnoreCase));
            if (match != null)
            {
                categories.Add(new UninstallCategoryWin($"registry-{hiveLabel}", $"Registry: {hiveLabel}\\{subPath}\\{match}",
                    new List<string> { $@"{hiveLabel}\{subPath}\{match}" }, 0, RequiresPrivilege: true));
            }
        }

        // Comenzi rapide (Start Menu + Desktop) — raman orfane dupa o
        // dezinstalare care nu le-a curatat singura.
        var shortcutRoots = new (string id, string title, string root)[]
        {
            ("startmenu-user", "Comenzi rapide (Start Menu, user)", Environment.GetFolderPath(Environment.SpecialFolder.StartMenu)),
            ("startmenu-common", CoreL.X("Comenzi rapide (Start Menu, toți userii)", "Shortcuts (Start Menu, all users)", "Accesos directos (menú Inicio, todos los usuarios)"), Environment.GetFolderPath(Environment.SpecialFolder.CommonStartMenu)),
            ("desktop-user", "Comenzi rapide (Desktop)", Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory)),
        };
        foreach (var (id, title, root) in shortcutRoots)
        {
            if (!Directory.Exists(root)) continue;
            List<string> matches;
            try
            {
                matches = Directory.GetFiles(root, "*.lnk", SearchOption.AllDirectories)
                    .Where(f => Path.GetFileNameWithoutExtension(f).Contains(name, StringComparison.OrdinalIgnoreCase))
                    .ToList();
            }
            catch { continue; }
            if (matches.Count == 0) continue;
            categories.Add(new UninstallCategoryWin(id, title, matches, matches.Sum(f => new FileInfo(f).Length), RequiresPrivilege: id == "startmenu-common"));
        }

        // Sarcini programate (Scheduled Tasks) — unele aplicatii isi
        // inregistreaza propriul updater/telemetrie acolo.
        try
        {
            var taskNames = Shell.Run("schtasks /query /fo LIST | findstr /B \"TaskName:\"");
            var matchingTasks = taskNames.Split('\n')
                .Select(l => l.Replace("TaskName:", "").Trim())
                .Where(t => t.Length > 0 && t.Contains(name, StringComparison.OrdinalIgnoreCase))
                .ToList();
            if (matchingTasks.Count > 0)
            {
                categories.Add(new UninstallCategoryWin("scheduledtasks", "Sarcini programate (Task Scheduler)", matchingTasks, 0, RequiresPrivilege: true));
            }
        }
        catch { /* schtasks indisponibil - degradeaza elegant */ }

        return categories;
    }

    private static long DirectorySize(string path)
    {
        try { return new DirectoryInfo(path).EnumerateFiles("*", SearchOption.AllDirectories).Sum(f => f.Length); }
        catch { return 0; }
    }

    /// Ruleaza dezinstalatorul oficial (din Registry `UninstallString`) —
    /// singura cale corectă de a scoate aplicația din "Apps & Features",
    /// nu doar ștergerea folderului.
    ///
    /// [2026-09-03] FIX REAL: `UninstallString` brut deschide de multe ori
    /// un WIZARD interactiv (Next/Uninstall/Finish) — mai ales dezastruos
    /// în ștergerea în masă (mai multe fereastre, fără nimeni să dea click
    /// prin ele). Multe dezinstalatoare NSIS/Inno se auto-relansează
    /// dintr-o copie temporară și procesul original iese aproape instant —
    /// `-Wait` se termină mult înainte ca ștergerea reală să se fi
    /// întâmplat, dând un fals "succes". Fix, în ordine de preferință:
    /// 1. `QuietUninstallString` (câmp Registry OPȚIONAL, dar populat de
    ///    multe installere tocmai pentru acest scop — silențios garantat).
    /// 2. Dacă `UninstallString` e un `msiexec.exe /X{GUID}` (MSI — ușor de
    ///    recunoscut, mereu suportă silent), adaugă automat `/quiet /norestart`.
    /// 3. Altfel, `UninstallString` brut, neschimbat — cel mai bun efort,
    ///    poate tot cere clickuri (limitare reală a instalatorului, nu de
    ///    cod), dar apelantul verifică acum REAL dacă a dispărut din
    ///    Registry (`IsStillRegistered`), nu doar codul de ieșire.
    public static bool RunOfficialUninstaller(InstalledAppWin app)
    {
        var cmd = BuildOfficialUninstallCommand(app);
        return cmd is not null && PrivilegedRunner.Run(cmd);
    }

    /// Comanda (PowerShell) care rulează dezinstalatorul oficial al aplicației; `null` dacă nu există.
    public static string? BuildOfficialUninstallCommand(InstalledAppWin app)
    {
        string? command = app.QuietUninstallString;
        if (string.IsNullOrWhiteSpace(command) && !string.IsNullOrWhiteSpace(app.UninstallString))
        {
            var raw = app.UninstallString!;
            if (raw.Contains("msiexec", StringComparison.OrdinalIgnoreCase))
            {
                // "/I{GUID}" sau "/X{GUID}" - fortam /X (uninstall) + silent,
                // indiferent ce a scris installerul (unele scriu /I gresit
                // pentru Modify/Repair, dar UninstallString e mereu pt. dezinstalare).
                var guidStart = raw.IndexOf('{');
                var guidEnd = raw.IndexOf('}');
                var guid = (guidStart >= 0 && guidEnd > guidStart) ? raw[guidStart..(guidEnd + 1)] : null;
                command = guid is not null ? $"msiexec.exe /X{guid} /quiet /norestart" : raw;
            }
            else
            {
                command = raw;
            }
        }
        return string.IsNullOrWhiteSpace(command) ? null : $"Start-Process -FilePath cmd.exe -ArgumentList '/c {command.Replace("'", "''")}' -Wait";
    }


    /// Cerinta (2026-09-01): "sa elimine tot tot tot" — extins sa recunoasca
    /// si categoriile noi (comenzi rapide = fisiere individuale, nu foldere;
    /// sarcini programate = nume, nu cale de disc/Registry), fiecare cu
    /// comanda de stergere corecta pentru tipul ei, nu doar Registry.
    public static void Delete(List<UninstallCategoryWin> categories, Action<string> log) =>
        UninstallBatch(new[] { new Target(new InstalledAppWin("", null, null), categories) }, log);

    /// O aplicație de dezinstalat, cu categoriile ei de urme asociate.
    public sealed record Target(InstalledAppWin App, List<UninstallCategoryWin> Categories);

    /// Dezinstalare pentru una sau mai multe aplicații ca UN SINGUR plan: urmele care merg cu drepturile utilizatorului se șterg întâi (fără prompt);
    /// comenzile privilegiate (Registry, sarcini programate, foldere de sistem) și dezinstalatoarele oficiale ale TUTUROR aplicațiilor formează UN plan,
    /// prezentat și autorizat o singură dată (un singur prompt UAC). Anulare/refuz = planul se oprește, fără prompturi succesive.
    public static void UninstallBatch(IReadOnlyList<Target> targets, Action<string> log)
    {
        var steps = new List<PrivilegedPlan.Step>();
        var uninstallers = new List<(InstalledAppWin App, string Label)>();
        var n = 0;
        foreach (var t in targets)
        {
            foreach (var category in t.Categories)
            {
                foreach (var path in category.Paths)
                {
                    string? cmd = null;
                    if (category.Id == "scheduledtasks") cmd = $"schtasks /delete /tn \"{path}\" /f";
                    else if (category.Id.StartsWith("registry")) cmd = $"Remove-Item -Path 'Registry::{path.Replace("'", "''")}' -Recurse -Force -ErrorAction SilentlyContinue";
                    else if (category.RequiresPrivilege)
                        cmd = File.Exists(path) ? $"Remove-Item -Path {PrivilegedPlan.Q(path)} -Force -ErrorAction SilentlyContinue" : $"Remove-Item -Path {PrivilegedPlan.Q(path)} -Recurse -Force -ErrorAction SilentlyContinue";
                    if (cmd is not null) { steps.Add(new PrivilegedPlan.Step($"{t.App.DisplayName}: {path}#{n++}", cmd)); continue; }
                    try
                    {
                        if (File.Exists(path)) File.Delete(path); else Directory.Delete(path, recursive: true);
                        log(CoreL.X($"Șters: {path}", $"Deleted: {path}", $"Eliminado: {path}"));
                    }
                    catch (Exception ex)
                    {
                        log(CoreL.X($"EROARE la ștergerea {path}: {ex.Message}", $"ERROR deleting {path}: {ex.Message}", $"ERROR al eliminar {path}: {ex.Message}"));
                    }
                }
            }
            if (!string.IsNullOrWhiteSpace(t.App.UninstallString) && BuildOfficialUninstallCommand(t.App) is { } uc)
            {
                var label = $"{t.App.DisplayName}: {CoreL.X("dezinstalator oficial", "official uninstaller", "desinstalador oficial")}#{n++}";
                steps.Add(new PrivilegedPlan.Step(label, uc)); uninstallers.Add((t.App, label));
            }
        }
        if (steps.Count == 0) return;
        log(CoreL.X($"Plan cu drepturi de administrator: {steps.Count} pași, o singură autorizare.", $"Administrator plan: {steps.Count} steps, a single authorisation.", $"Plan con derechos de administrador: {steps.Count} pasos, una sola autorización."));
        var outcome = PrivilegedPlan.Execute(CoreL.X("Dezinstalare cu drepturi de administrator", "Uninstall with administrator rights", "Desinstalación con derechos de administrador"), steps);
        if (outcome.Cancelled) { log(CoreL.X("Anulat: nu s-a modificat nimic din planul privilegiat și nu se mai cere nicio autorizare.", "Cancelled: nothing in the privileged plan was changed and no further authorisation will be requested.", "Cancelado: no se modificó nada del plan con privilegios y no se pedirá ninguna otra autorización.")); return; }
        foreach (var s in steps.Where(x => !uninstallers.Any(u => u.Label == x.Label)))
            if (outcome.Results.TryGetValue(s.Label, out var err) && err is not null) log($"EROARE {s.Label}: {err}");
        foreach (var (app, label) in uninstallers)
        {
            if (outcome.Results[label] is not null) log(CoreL.X($"EROARE la rularea dezinstalatorului oficial pentru {app.DisplayName}.", $"ERROR running the official uninstaller for {app.DisplayName}.", $"ERROR al ejecutar el desinstalador oficial de {app.DisplayName}."));
            else if (IsStillRegistered(app.DisplayName)) log(CoreL.X($"⚠ {app.DisplayName}: tot apare instalată — probabil dezinstalatorul ei arată o fereastră proprie care așteaptă un click.", $"⚠ {app.DisplayName}: still shows as installed — its uninstaller probably shows its own window waiting for a click.", $"⚠ {app.DisplayName}: sigue apareciendo instalada: su desinstalador probablemente muestra una ventana propia que espera un clic."));
            else log(CoreL.X($"✔ {app.DisplayName}: dezinstalator oficial rulat — dispărută din Programe și caracteristici.", $"✔ {app.DisplayName}: official uninstaller ran — gone from Programs and Features.", $"✔ {app.DisplayName}: desinstalador oficial ejecutado; ya no aparece en Programas y características."));
        }
    }
}
