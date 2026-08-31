namespace MacMasterControlPro.Core.Services;

/// Port 1:1 al ResolveConfigSyncService.swift (Mac) - foldere de
/// configurare Resolve/Fusion portabile intre statii (LUT-uri, macro-uri,
/// sabloane, script-uri Fusion - fisiere simple, NU PowerGrade-urile, care
/// traiesc in baza de date interna Resolve - TODO separat, vezi Mac).
public sealed class ResolveConfigFolder
{
    public required string Label { get; init; }
    public required string Path { get; init; }
    public bool Exists => Directory.Exists(Path);
}

public static class ResolveConfigSyncService
{
    private static readonly string Base =
        System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            "Blackmagic Design", "DaVinci Resolve", "Support");

    public static List<ResolveConfigFolder> Folders => new()
    {
        new ResolveConfigFolder { Label = "LUT-uri (LUT)", Path = System.IO.Path.Combine(Base, "LUT") },
        new ResolveConfigFolder { Label = "Fusion — Macro-uri", Path = System.IO.Path.Combine(Base, "Fusion", "Macros") },
        new ResolveConfigFolder { Label = "Fusion — Șabloane", Path = System.IO.Path.Combine(Base, "Fusion", "Templates") },
        new ResolveConfigFolder { Label = "Fusion — Script-uri", Path = System.IO.Path.Combine(Base, "Fusion", "Scripts") },
    };
}
