namespace MacMasterControlPro.Core.Services.Access;

/// În Demo, aplicația pornește de fiecare dată de la zero: setările utilizatorului și cache-urile aferente nu se păstrează.
/// Nu se ating: starea de acces (secrete DPAPI), instantaneul Modului Randare (siguranța sistemului) și jurnalele.
public static class DemoProfile
{
    private static readonly string[] KeepFiles = { "RenderModeSnapshot.json" };
    private static readonly string[] KeepDirs = { "secrets" };

    public static void Wipe(string? roamingDir = null, string? localDir = null)
    {
        roamingDir ??= Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "MacMasterControlPro");
        localDir ??= Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MacMasterControlPro");
        Clear(roamingDir, KeepFiles, KeepDirs);
        Clear(localDir, Array.Empty<string>(), new[] { "Logs" });   // include DiskCache; jurnalele rămân
        SecretStore.Set("smtp.appPassword", "");
        DiagnosticLog.Log("info", "access", "demo.profile-wiped");
    }

    private static void Clear(string dir, string[] keepFiles, string[] keepDirs)
    {
        if (!Directory.Exists(dir)) return;
        foreach (var f in Directory.GetFiles(dir)) if (!keepFiles.Contains(Path.GetFileName(f))) { try { File.Delete(f); } catch { } }
        foreach (var d in Directory.GetDirectories(dir)) if (!keepDirs.Contains(Path.GetFileName(d))) { try { Directory.Delete(d, true); } catch { } }
    }
}
