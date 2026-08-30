namespace MacMasterControlPro.Core.Services;

/// Oglinda LicenseState.swift (Mac): trial NELIMITAT pentru analize/scanari
/// (fara numarare de zile, spre deosebire de GDCVault) - doar actiunile
/// care scriu pe disc/sistem (curatare, tuning, montare Cloud, eliminare
/// Rosetta) verifica IsUnlocked. Activare printr-un cod generat manual din
/// Furnizor (GenerateSerialView.swift, Mac), `mac-master-control-pro` in
/// `gdcStandaloneProducts`.
public sealed class LicenseManager
{
    public static readonly LicenseManager Shared = new();
    public const string ProductId = "mac-master-control-pro";

    public bool IsLicensed { get; private set; }
    public long LicenseExpiresAt { get; private set; } // 0 = perpetuu
    public bool LicenseMachineLocked { get; private set; }
    public string? ActivationError { get; private set; }

    public event Action? Changed;

    private static string ActivationFilePath =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Master Control Studio Pro", "license.txt");

    private LicenseManager() => LoadSavedLicense();

    /// Doar pentru afisare (Sidebar Footer) — codul salvat, daca exista.
    public string? SavedLicenseCode
    {
        get
        {
            var path = ActivationFilePath;
            return File.Exists(path) ? File.ReadAllText(path).Trim() : null;
        }
    }

    /// Verificat inainte de orice actiune de scriere (nu si scanari/analize).
    public bool IsUnlocked => IsLicensed;

    public bool Activate(string code)
    {
        ActivationError = null;
        var trimmed = code.Trim();
        try
        {
            var payload = LicenseCore.Validate(trimmed, ProductId);
            SaveLicense(trimmed);
            ApplyLicense(payload.ExpiresAt, payload.MachineLocked);
            Changed?.Invoke();
            return true;
        }
        catch (LicenseCore.ValidationError error)
        {
            ActivationError = MessageFor(error.Kind);
            Changed?.Invoke();
            return false;
        }
    }

    public void Deactivate()
    {
        IsLicensed = false;
        LicenseExpiresAt = 0;
        LicenseMachineLocked = false;
        var path = ActivationFilePath;
        if (File.Exists(path)) File.Delete(path);
        Changed?.Invoke();
    }

    private void LoadSavedLicense()
    {
        var path = ActivationFilePath;
        if (!File.Exists(path)) return;
        var code = File.ReadAllText(path).Trim();
        try
        {
            var payload = LicenseCore.Validate(code, ProductId);
            ApplyLicense(payload.ExpiresAt, payload.MachineLocked);
        }
        catch (LicenseCore.ValidationError)
        {
            // Cod salvat invalid/expirat — ramanem nelicentiati, fara sa aruncam mai departe.
        }
    }

    private void ApplyLicense(long expiresAt, bool machineLocked)
    {
        IsLicensed = true;
        LicenseExpiresAt = expiresAt;
        LicenseMachineLocked = machineLocked;
    }

    private static void SaveLicense(string code)
    {
        var path = ActivationFilePath;
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, code);
    }

    private static string MessageFor(LicenseCore.ValidationErrorKind kind) => kind switch
    {
        LicenseCore.ValidationErrorKind.MalformedCode => "Cod invalid — verifică să nu lipsească vreun caracter.",
        LicenseCore.ValidationErrorKind.BadSignature => "Semnătura codului nu se potrivește.",
        LicenseCore.ValidationErrorKind.WrongProduct => "Codul e valid, dar pentru alt produs GDC.",
        LicenseCore.ValidationErrorKind.WrongMachine => "Codul e blocat pe alt calculator.",
        LicenseCore.ValidationErrorKind.Expired => "Codul a expirat.",
        _ => "Cod invalid.",
    };
}
