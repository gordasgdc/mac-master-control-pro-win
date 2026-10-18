namespace MacMasterControlPro.Core.Services.Access;

/// Resetarea de probă („de la zero”) se aplică NUMAI profilului izolat (build demonstrativ/test) sau unor ținte date explicit (teste).
/// Pe profilul oficial nu face NIMIC: proba nu șterge niciodată setările, conturile, credențialele sau datele utilizatorului
/// (hotărâre proprietar, 2026-10-18). Starea de acces (secrets) și instantaneul Modului Randare nu se ating nici în profil izolat.
public static class DemoProfile
{
    private static readonly string[] KeepFiles = { "RenderModeSnapshot.json" };
    private static readonly string[] KeepDirs = { "secrets", "Logs" };

    public static void Wipe(string? roamingDir = null, string? localDir = null)
    {
        var explicitTargets = roamingDir is not null || localDir is not null;
        if (!AppProfile.IsIsolated && !explicitTargets) { DiagnosticLog.Log("info", "access", "demo.wipe-skipped-official-profile"); return; }
        roamingDir ??= AppProfile.Roaming("MacMasterControlPro");
        localDir ??= AppProfile.Local("MacMasterControlPro");
        Clear(roamingDir, KeepFiles, KeepDirs);
        Clear(localDir, Array.Empty<string>(), KeepDirs);
        if (!explicitTargets) { Clear(AppProfile.Local("Master Control Studio Pro"), Array.Empty<string>(), KeepDirs); SecretStore.Set("smtp.appPassword", ""); }
        DiagnosticLog.Log("info", "access", "demo.profile-wiped");
    }

    private static void Clear(string dir, string[] keepFiles, string[] keepDirs)
    {
        if (!Directory.Exists(dir)) return;
        foreach (var f in Directory.GetFiles(dir)) if (!keepFiles.Contains(Path.GetFileName(f))) { try { File.Delete(f); } catch { } }
        foreach (var d in Directory.GetDirectories(dir)) if (!keepDirs.Contains(Path.GetFileName(d))) { try { Directory.Delete(d, true); } catch { } }
    }
}
