using System.IO;
using System.Text.Json;
using Microsoft.Win32;

namespace MacMasterControlPro.Core.Services;

/// Port 1:1 al LoginItemsService.swift (Mac) - agenti de fundal terti la
/// pornire, gasiti in cheile Registry Run + folderul Startup (nu componente
/// Microsoft - excluse explicit, la fel ca `com.apple.*` pe Mac).
public sealed class LoginItem
{
    public required string Name { get; init; }
    public required string Command { get; init; }
    public required bool IsHKLM { get; init; } // HKLM = necesita admin
    public required string Source { get; init; } // "Registry" | "Startup"
}

public static class LoginItemsService
{
    private static readonly string DisabledStorePath =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "MacMasterControlPro", "DisabledLoginItems.json");
    private static readonly string StartupHoldingDir =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "MacMasterControlPro", "DisabledStartupItems");

    private const string RunKeyPath = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";

    public static List<LoginItem> Scan()
    {
        var items = new List<LoginItem>();
        AddFromHive(items, Registry.CurrentUser, isHKLM: false);
        AddFromHive(items, Registry.LocalMachine, isHKLM: true);

        var startupDir = Environment.GetFolderPath(Environment.SpecialFolder.Startup);
        if (Directory.Exists(startupDir))
        {
            foreach (var file in Directory.GetFiles(startupDir, "*.lnk"))
            {
                items.Add(new LoginItem { Name = Path.GetFileNameWithoutExtension(file), Command = file, IsHKLM = false, Source = "Startup" });
            }
        }
        return items.OrderBy(i => i.Name).ToList();
    }

    private static void AddFromHive(List<LoginItem> items, RegistryKey hive, bool isHKLM)
    {
        using var key = hive.OpenSubKey(RunKeyPath);
        if (key is null) return;
        foreach (var name in key.GetValueNames())
        {
            if (name.StartsWith("Microsoft", StringComparison.OrdinalIgnoreCase)) continue; // filtrare componente Microsoft cunoscute
            var value = key.GetValue(name) as string ?? "";
            items.Add(new LoginItem { Name = name, Command = value, IsHKLM = isHKLM, Source = "Registry" });
        }
    }

    /// Dezactiveaza (salveaza in carantina + sterge din Run/muta din Startup) -
    /// reversibil oricand din `Enable`.
    public static void Disable(LoginItem item, Action<string> log)
    {
        if (item.Source == "Startup")
        {
            Directory.CreateDirectory(StartupHoldingDir);
            var dest = Path.Combine(StartupHoldingDir, Path.GetFileName(item.Command));
            log($"$ move \"{item.Command}\" \"{dest}\"");
            try { File.Move(item.Command, dest, overwrite: true); log($"✔ {item.Name} mutat în carantină."); }
            catch (Exception ex) { log($"✘ {ex.Message}"); }
            return;
        }

        SaveToQuarantine(item);
        if (item.IsHKLM)
        {
            var cmd = $"Remove-ItemProperty -Path 'HKLM:\\{RunKeyPath}' -Name '{item.Name}' -ErrorAction SilentlyContinue";
            log($"$ {cmd}");
            if (!PrivilegedRunner.Run(cmd, line => log("  " + line))) { log("✘ Promptul UAC a fost respins."); return; }
        }
        else
        {
            log($"$ Remove-ItemProperty HKCU:\\...\\Run -Name '{item.Name}'");
            using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: true);
            key?.DeleteValue(item.Name, throwOnMissingValue: false);
        }
        log($"✔ {item.Name} dezactivat — reversibil din „Reactivează”.");
    }

    public static void Enable(LoginItem item, Action<string> log)
    {
        if (item.Source == "Startup")
        {
            var quarantined = Path.Combine(StartupHoldingDir, Path.GetFileName(item.Command));
            var startupDir = Environment.GetFolderPath(Environment.SpecialFolder.Startup);
            var dest = Path.Combine(startupDir, Path.GetFileName(item.Command));
            log($"$ move \"{quarantined}\" \"{dest}\"");
            try { File.Move(quarantined, dest, overwrite: true); log($"✔ {item.Name} reactivat."); }
            catch (Exception ex) { log($"✘ {ex.Message}"); }
            return;
        }

        var saved = LoadQuarantine().FirstOrDefault(q => q.Name == item.Name);
        var value = saved?.Command ?? item.Command;
        if (item.IsHKLM)
        {
            var cmd = $"Set-ItemProperty -Path 'HKLM:\\{RunKeyPath}' -Name '{item.Name}' -Value '{value}'";
            log($"$ {cmd}");
            if (!PrivilegedRunner.Run(cmd, line => log("  " + line))) { log("✘ Promptul UAC a fost respins."); return; }
        }
        else
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: true) ?? Registry.CurrentUser.CreateSubKey(RunKeyPath);
            key.SetValue(item.Name, value);
        }
        RemoveFromQuarantine(item.Name);
        log($"✔ {item.Name} reactivat.");
    }

    public static HashSet<string> DisabledNames() => LoadQuarantine().Select(q => q.Name).ToHashSet();

    private sealed class QuarantinedItem { public string Name { get; set; } = ""; public string Command { get; set; } = ""; }

    private static List<QuarantinedItem> LoadQuarantine()
    {
        if (!File.Exists(DisabledStorePath)) return new();
        try { return JsonSerializer.Deserialize<List<QuarantinedItem>>(File.ReadAllText(DisabledStorePath)) ?? new(); }
        catch { return new(); }
    }

    private static void SaveToQuarantine(LoginItem item)
    {
        var list = LoadQuarantine().Where(q => q.Name != item.Name).ToList();
        list.Add(new QuarantinedItem { Name = item.Name, Command = item.Command });
        Directory.CreateDirectory(Path.GetDirectoryName(DisabledStorePath)!);
        File.WriteAllText(DisabledStorePath, JsonSerializer.Serialize(list));
    }

    private static void RemoveFromQuarantine(string name)
    {
        var list = LoadQuarantine().Where(q => q.Name != name).ToList();
        File.WriteAllText(DisabledStorePath, JsonSerializer.Serialize(list));
    }
}
