using System.IO;
using System.Text.Json;
using Microsoft.Win32;

namespace MacMasterControlPro.Core.Services;

/// O locatie (disc sau folder ales manual) gestionata de Spotlight Shield
/// (Windows Search) — port 1:1 al `SpotlightTarget` (Mac), regula globala
/// de multi-selectie (2026-08-30).
public sealed record SpotlightTarget(string Name, string Path, bool IsVolume);

/// Oglinda TweaksService.swift (Mac) — Explorer in loc de Finder,
/// registry in loc de `defaults write`.
public sealed class TweaksService
{
    private static string CustomFoldersPath => System.IO.Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "MacMasterControlPro", "spotlight-shield-folders.json");

    /// Discuri fixe/detasabile (exclus discul de sistem, C:\ tipic) +
    /// folderele custom persistate.
    public List<SpotlightTarget> ScanSpotlightTargets()
    {
        var targets = new List<SpotlightTarget>();
        var systemDrive = System.IO.Path.GetPathRoot(Environment.GetFolderPath(Environment.SpecialFolder.Windows));

        foreach (var drive in DriveInfo.GetDrives())
        {
            if (!drive.IsReady) continue;
            if (string.Equals(drive.RootDirectory.FullName, systemDrive, StringComparison.OrdinalIgnoreCase)) continue;
            var label = string.IsNullOrWhiteSpace(drive.VolumeLabel) ? drive.Name : $"{drive.VolumeLabel} ({drive.Name})";
            targets.Add(new SpotlightTarget(label, drive.RootDirectory.FullName, IsVolume: true));
        }

        foreach (var path in LoadCustomFolders())
        {
            if (!Directory.Exists(path)) continue;
            targets.Add(new SpotlightTarget(new DirectoryInfo(path).Name, path, IsVolume: false));
        }

        return targets;
    }

    public void AddCustomFolders(IEnumerable<string> paths)
    {
        var stored = LoadCustomFolders();
        foreach (var path in paths)
        {
            if (!stored.Contains(path, StringComparer.OrdinalIgnoreCase)) stored.Add(path);
        }
        SaveCustomFolders(stored);
    }

    public void RemoveCustomFolder(string path)
    {
        var stored = LoadCustomFolders();
        stored.RemoveAll(p => string.Equals(p, path, StringComparison.OrdinalIgnoreCase));
        SaveCustomFolders(stored);
        SetProtected(path, false);
    }

    private List<string> LoadCustomFolders()
    {
        try
        {
            if (!File.Exists(CustomFoldersPath)) return [];
            return JsonSerializer.Deserialize<List<string>>(File.ReadAllBytes(CustomFoldersPath)) ?? [];
        }
        catch { return []; }
    }

    private void SaveCustomFolders(List<string> folders)
    {
        try
        {
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(CustomFoldersPath)!);
            File.WriteAllBytes(CustomFoldersPath, JsonSerializer.SerializeToUtf8Bytes(folders));
        }
        catch { /* Nescrierea nu trebuie sa blocheze sesiunea curenta. */ }
    }

    public bool IsProtected(string path)
    {
        try { return Directory.Exists(path) && File.GetAttributes(path).HasFlag(FileAttributes.NotContentIndexed); }
        catch { return false; }
    }

    public bool SetProtected(string path, bool protect)
    {
        if (!Directory.Exists(path)) return false;
        try
        {
            var attrs = File.GetAttributes(path);
            File.SetAttributes(path, protect ? attrs | FileAttributes.NotContentIndexed : attrs & ~FileAttributes.NotContentIndexed);
            return true;
        }
        catch { return false; }
    }

    /// Aplica protectia pe exact setul bifat (Bara de Actiune in Masa) —
    /// dezactiveaza restul, la fel ca pe Mac.
    public void ApplyProtection(IReadOnlyList<SpotlightTarget> targets, ISet<string> selected)
    {
        foreach (var target in targets) SetProtected(target.Path, selected.Contains(target.Path));
    }

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

}
