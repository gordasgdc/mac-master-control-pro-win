using System.Diagnostics;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;

namespace MacMasterControlPro.Core.Services;

/// Izolarea profilului. Aplicația instalată (MacMasterControlPro.exe) folosește profilul oficial; ORICE alt proces (build de test/demonstrativ,
/// runner de teste, sau `MCSP_PROFILE_DIR` setat) folosește un profil SEPARAT: directoare, secrete, jurnale, chei de registru.
/// Un proces izolat nu citește și nu modifică niciodată profilul oficial.
public static class AppProfile
{
    public const string OfficialProcessName = "MacMasterControlPro";
    public static string? OverrideProcessName { get; set; }     // doar teste
    public static string? OverrideRoot { get; set; }            // doar teste

    private static string ProcessName => OverrideProcessName ?? Process.GetCurrentProcess().ProcessName;
    private static string? ExplicitRoot => OverrideRoot ?? (Environment.GetEnvironmentVariable("MCSP_PROFILE_DIR") is { Length: > 0 } p ? p : null);
    public static bool IsIsolated => ExplicitRoot is not null || !string.Equals(ProcessName, OfficialProcessName, StringComparison.OrdinalIgnoreCase);

    private static string IsolationId => new string(ProcessName.Select(c => char.IsLetterOrDigit(c) || c == '.' ? c : '_').ToArray());
    private static string RootTag => ExplicitRoot is { } r ? "." + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(r)))[..6].ToLowerInvariant() : "";

    public static string IsolatedRoot => ExplicitRoot ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MCSP-Isolated", IsolationId);

    /// %APPDATA%\<name> pentru profilul oficial; în profilul izolat, `<rădăcină>\Roaming\<name>`.
    public static string Roaming(string name) => IsIsolated
        ? Path.Combine(IsolatedRoot, "Roaming", name)
        : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), name);

    /// %LOCALAPPDATA%\<name> pentru profilul oficial; în profilul izolat, `<rădăcină>\Local\<name>`.
    public static string Local(string name) => IsIsolated
        ? Path.Combine(IsolatedRoot, "Local", name)
        : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), name);

    public static string OfficialRoaming(string name) => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), name);
    public static string OfficialLocal(string name) => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), name);

    /// Cheie de registru a stării de acces; în profil izolat primește sufix, ca să nu atingă cheia oficială.
    public static string RegistryKey(string officialSubKey) => IsIsolated ? officialSubKey + "-iso-" + IsolationId + RootTag : officialSubKey;

    /// Directoarele de date ale configurației utilizatorului (profilul curent).
    public static IEnumerable<string> ConfigDirectories() => new[] { Roaming("MacMasterControlPro"), Local("MacMasterControlPro"), Local("Master Control Studio Pro") };

    public static List<string> ResetPreview() => new()
    {
        CoreL.X("Setările aplicației (temă, limbă, mărimea textului, folderele alese, profil)", "App settings (theme, language, text size, chosen folders, profile)", "Ajustes de la app (tema, idioma, tamaño del texto, carpetas elegidas, perfil)"),
        CoreL.X("Cache-uri și fișiere locale din directoarele de date ale aplicației", "Caches and local files in the app data folders", "Cachés y archivos locales en las carpetas de datos de la app"),
        CoreL.X("Parola de aplicație SMTP (dacă a fost salvată)", "SMTP app password (if saved)", "Contraseña de aplicación SMTP (si se guardó)"),
        CoreL.X("NU se șterg: licența/starea de acces, conturile rclone, jurnalele, instantaneul Modului Randare", "NOT deleted: the licence/access state, rclone accounts, logs, the Render Mode snapshot", "NO se borran: la licencia y el estado de acceso, las cuentas de rclone, los registros y la instantánea del modo de renderizado"),
    };

    /// Resetarea configurației utilizatorului, cu COPIE DE SIGURANȚĂ (zip) făcută înainte. Se cheamă DOAR dintr-o acțiune explicită a utilizatorului.
    public static string ResetUserConfiguration(string? backupRoot = null)
    {
        backupRoot ??= Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MacMasterControlPro-Backups");
        Directory.CreateDirectory(backupRoot);
        var zip = Path.Combine(backupRoot, $"config-{DateTime.Now:yyyyMMdd-HHmmss}.zip");
        using (var z = ZipFile.Open(zip, ZipArchiveMode.Create))
            foreach (var d in ConfigDirectories().Where(Directory.Exists))
                foreach (var f in Directory.EnumerateFiles(d, "*", SearchOption.AllDirectories).Where(f => !f.Contains(Path.DirectorySeparatorChar + "secrets" + Path.DirectorySeparatorChar) && !f.Contains(Path.DirectorySeparatorChar + "Logs" + Path.DirectorySeparatorChar)))
                    try { z.CreateEntryFromFile(f, Path.GetRelativePath(Path.GetDirectoryName(d)!, f)); } catch { }
        foreach (var d in ConfigDirectories().Where(Directory.Exists))
        {
            foreach (var f in Directory.GetFiles(d)) if (Path.GetFileName(f) != "RenderModeSnapshot.json") try { File.Delete(f); } catch { }
            foreach (var s in Directory.GetDirectories(d)) if (Path.GetFileName(s) is not ("secrets" or "Logs")) try { Directory.Delete(s, true); } catch { }
        }
        SecretStore.Set("smtp.appPassword", "");
        DiagnosticLog.Log("warning", "profile", "config.reset-by-user");
        return zip;
    }
}
