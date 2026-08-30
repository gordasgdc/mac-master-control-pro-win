using Microsoft.Win32;

namespace MacMasterControlPro.Core.Services;

/// Oglinda TweaksService.swift (Mac) — Explorer in loc de Finder,
/// registry in loc de `defaults write`.
public sealed class TweaksService
{
    public void EnableExplorerAdvancedView()
    {
        using var key = Registry.CurrentUser.OpenSubKey(
            @"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced", writable: true);
        key?.SetValue("Hidden", 1, RegistryValueKind.DWord);
        key?.SetValue("HideFileExt", 0, RegistryValueKind.DWord);
        key?.SetValue("ShowSuperHidden", 1, RegistryValueKind.DWord);
        Shell.Run("Stop-Process -ProcessName explorer -Force");
    }

    /// Echivalent .DS_Store: dezactiveaza thumbs.db pe retea (nu exista un
    /// comutator per-USB dedicat ca pe Mac - politica Explorer se aplica
    /// global pentru unitatile de retea).
    public void BlockThumbsDbOnNetworkDrives()
    {
        using var key = Registry.CurrentUser.OpenSubKey(
            @"Software\Microsoft\Windows\CurrentVersion\Explorer", writable: true);
        key?.SetValue("DisableThumbsDBOnNetworkFolders", 1, RegistryValueKind.DWord);
    }

    /// Windows Search Index exclusion pentru un folder ales — echivalent
    /// Spotlight Shield. FileAttributes.NotContentIndexed e atributul
    /// standard recunoscut de Windows Search (acelasi ca in Proprietati
    /// folder -> Avansat -> "Allow files... to be indexed" debifat).
    public bool ProtectFromIndexing(string folderPath)
    {
        if (!Directory.Exists(folderPath)) return false;
        try
        {
            File.SetAttributes(folderPath, File.GetAttributes(folderPath) | FileAttributes.NotContentIndexed);
            return true;
        }
        catch { return false; }
    }
}
