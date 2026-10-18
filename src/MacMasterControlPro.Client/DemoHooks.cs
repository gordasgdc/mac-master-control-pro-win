using MacMasterControlPro.Core.Services;
namespace MacMasterControlPro.Client;

/// Ganci pentru materiale demonstrative (capturi): limba, tema si pagina de pornire din variabile de mediu.
/// Active NUMAI intr-un profil izolat (`MCSP_PROFILE_DIR`); in instalarea oficiala nu au niciun efect.
public static class DemoHooks
{
    private static string? Get(string name) => AppProfile.IsIsolated && Environment.GetEnvironmentVariable(name) is { Length: > 0 } v ? v : null;
    public static string? Language => Get("MCSP_DEMO_LANG");
    public static string? Theme => Get("MCSP_DEMO_THEME");
    public static string? Page => Get("MCSP_DEMO_PAGE");
}
