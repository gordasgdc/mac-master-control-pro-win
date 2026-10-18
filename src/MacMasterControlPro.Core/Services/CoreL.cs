namespace MacMasterControlPro.Core.Services;

/// Limba pentru textele produse de nucleu. Aplicatia o seteaza la pornire si la schimbarea limbii ("ro" | "en" | "es").
public static class CoreL
{
    public static volatile string Code = "ro";
    public static string X(string ro, string en, string es) => Code switch { "en" => en, "es" => es, _ => ro };
}
