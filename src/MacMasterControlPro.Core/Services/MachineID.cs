using System.Management;
using System.Runtime.Versioning;
using System.Security.Cryptography;
using System.Text;

namespace MacMasterControlPro.Core.Services;

/// Port al MachineID.swift pentru Windows — acelasi principiu (un ID hardware
/// stabil, SHA-512, primii 6 octeti, Base32 fara liniute) dar sursa ID-ului
/// e diferita: pe Mac e IOPlatformUUID (IOKit), pe Windows e UUID-ul din
/// Win32_ComputerSystemProduct (expus de BIOS/placa de baza prin WMI) —
/// la fel de stabil intre reporniri/reinstalari OS, legat de placa de baza,
/// nu de disc. NU produce acelasi hash ca pe Mac pentru aceeasi masina
/// fizica (surse diferite) — asta e de asteptat: fiecare platforma isi are
/// propriul spatiu de coduri machine-locked, generate separat din Furnizor.
[SupportedOSPlatform("windows")]
public static class MachineID
{
    /// UUID-ul hardware raportat de Windows — stabil intre reporniri.
    private static string RawPlatformUuid()
    {
        try
        {
            using var searcher = new ManagementObjectSearcher("SELECT UUID FROM Win32_ComputerSystemProduct");
            foreach (var obj in searcher.Get())
            {
                var uuid = obj["UUID"]?.ToString();
                if (!string.IsNullOrWhiteSpace(uuid) && uuid != "FFFFFFFF-FFFF-FFFF-FFFF-FFFFFFFFFFFF")
                {
                    return uuid;
                }
            }
        }
        catch
        {
            // WMI indisponibil (rulare fara privilegii, VM restrictionata, etc.)
        }
        return "win-machine-id-unavailable";
    }

    /// Hash-ul de 6 octeti folosit atat pentru afisare cat si pentru
    /// machine-locking-ul codurilor de licenta.
    public static byte[] HashBytes =>
        SHA512.HashData(Encoding.UTF8.GetBytes(RawPlatformUuid()))[..6];

    /// String Base32 scurt, lizibil (fara liniute) — ce copiaza userul din
    /// Preferinte -> Licenta si trimite inainte sa cumpere.
    public static string Display => LicenseCore.Base32Encode(HashBytes);
}
