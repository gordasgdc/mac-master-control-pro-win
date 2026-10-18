using System.Security.Cryptography;
using System.Text;

namespace MacMasterControlPro.Core.Services;

/// Secrete protejate cu DPAPI (DataProtectionScope.CurrentUser), fisier per cont in AppData.
/// Valorile nu se logheaza. Echivalentul nativ Windows al Keychain-ului de pe Mac.
public static class SecretStore
{
    private static string PathFor(string account) => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "MacMasterControlPro", "secrets",
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(account)))[..16] + ".bin");
    private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("com.gordasgdc.macmastercontrolpro.secrets");

    public static bool Set(string account, string value)
    {
        try
        {
            var p = PathFor(account);
            if (string.IsNullOrEmpty(value)) { if (File.Exists(p)) File.Delete(p); return true; }
            Directory.CreateDirectory(Path.GetDirectoryName(p)!);
            File.WriteAllBytes(p, ProtectedData.Protect(Encoding.UTF8.GetBytes(value), Entropy, DataProtectionScope.CurrentUser));
            return true;
        }
        catch (Exception ex) { DiagnosticLog.Log("error", "secrets", "set.fail", ex.GetType().Name); return false; }
    }

    public static string? Get(string account)
    {
        try
        {
            var p = PathFor(account);
            if (!File.Exists(p)) return null;
            return Encoding.UTF8.GetString(ProtectedData.Unprotect(File.ReadAllBytes(p), Entropy, DataProtectionScope.CurrentUser));
        }
        catch { return null; }
    }
}
