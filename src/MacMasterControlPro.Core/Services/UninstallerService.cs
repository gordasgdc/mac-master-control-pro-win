using Microsoft.Win32;

namespace MacMasterControlPro.Core.Services;

/// Oglinda UninstallerService.swift (Mac) — scaneaza Registry (Uninstall
/// keys, sursa oficiala Windows a listei de aplicatii instalate) + toate
/// locatiile standard unde o aplicatie isi lasa urme.
public sealed record InstalledAppWin(string DisplayName, string? UninstallString, string? InstallLocation);

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

    public static List<InstalledAppWin> ScanInstalledApps()
    {
        var results = new List<InstalledAppWin>();
        foreach (var keyPath in UninstallRegistryKeys)
        {
            using var key = Registry.LocalMachine.OpenSubKey(keyPath);
            if (key is null) continue;
            foreach (var subName in key.GetSubKeyNames())
            {
                using var sub = key.OpenSubKey(subName);
                var name = sub?.GetValue("DisplayName") as string;
                if (string.IsNullOrWhiteSpace(name)) continue;
                results.Add(new InstalledAppWin(
                    name,
                    sub?.GetValue("UninstallString") as string,
                    sub?.GetValue("InstallLocation") as string));
            }
        }
        return results.DistinctBy(a => a.DisplayName)
            .OrderBy(a => a.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToList();
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

        // Registry HKCU\Software\<Nume> — enumerare read-only, fara privilegiu.
        using (var software = Registry.CurrentUser.OpenSubKey(@"Software"))
        {
            var match = software?.GetSubKeyNames().FirstOrDefault(n => n.Contains(name, StringComparison.OrdinalIgnoreCase));
            if (match != null)
            {
                categories.Add(new UninstallCategoryWin("registry", $"Registry: HKCU\\Software\\{match}",
                    new List<string> { $@"HKCU\Software\{match}" }, 0, RequiresPrivilege: true));
            }
        }

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
    public static bool RunOfficialUninstaller(InstalledAppWin app)
    {
        if (string.IsNullOrWhiteSpace(app.UninstallString)) return false;
        return PrivilegedRunner.Run($"Start-Process -FilePath cmd.exe -ArgumentList '/c {app.UninstallString.Replace("'", "''")}' -Wait");
    }

    public static void Delete(List<UninstallCategoryWin> categories, Action<string> log)
    {
        var privileged = new List<string>();
        foreach (var category in categories)
        {
            foreach (var path in category.Paths)
            {
                if (category.RequiresPrivilege)
                {
                    privileged.Add($"Remove-Item -Path 'Registry::{path}' -Recurse -Force -ErrorAction SilentlyContinue");
                }
                else
                {
                    try
                    {
                        Directory.Delete(path, recursive: true);
                        log($"Șters: {path}");
                    }
                    catch (Exception ex)
                    {
                        log($"EROARE la ștergerea {path}: {ex.Message}");
                    }
                }
            }
        }
        if (privileged.Count > 0)
        {
            log($"Solicit privilegii de administrator pentru {privileged.Count} chei de Registry…");
            var ok = PrivilegedRunner.Run(privileged);
            log(ok ? "Registry curățat." : "EROARE la curățarea Registry.");
        }
    }
}
