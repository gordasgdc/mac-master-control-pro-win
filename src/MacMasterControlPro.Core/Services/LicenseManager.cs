using MacMasterControlPro.Core.Services.Access;

namespace MacMasterControlPro.Core.Services;

/// Fațadă pentru codul existent al paginilor: `IsUnlocked` = funcțiile sunt permise acum (licență sau sesiune de probă disponibilă).
/// Logica reală: `IAccessProvider` (ediția comunitară sau cea oficială). Fără Trial nelimitat, fără seriale vechi.
public sealed class LicenseManager
{
    public static readonly LicenseManager Shared = new();
    public const string ProductId = "mac-master-control-pro";

    private readonly IAccessProvider _provider;
    public event Action? Changed;

    private LicenseManager()
    {
#if OFFICIAL_BUILD
        AccessFactory.Make = () => new MacMasterControlPro.Official.OfficialAccess();
#endif
        _provider = AccessFactory.Make();
        _provider.Changed += () => Changed?.Invoke();
    }

    public IAccessProvider Provider => _provider;
    public AccessMode Mode => _provider.Mode;
    public bool IsUnlocked => _provider.Mode.AllowsFeatures;
    public bool IsLicensed => _provider.Mode.IsFull;
}
