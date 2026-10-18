using System.Text.Json;
using Microsoft.Win32;

namespace MacMasterControlPro.Core.Services;

/// Starea sistemului INAINTE de Modul Randare, salvata pe disc inainte de orice modificare:
/// dezactivarea readuce exact ce era (nu forteaza `delayed-auto` si nu porneste servicii care erau oprite),
/// iar o inchidere fortata nu lasa sistemul modificat fara urma.
public sealed class RenderModeSnapshot
{
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    /// nume serviciu -> stare; lipsa din dictionar = necunoscut (nu se atinge)
    public Dictionary<string, ServiceState> Services { get; set; } = new();
    /// pid -> ProcessPriorityClass dinainte
    public Dictionary<int, string> Priorities { get; set; } = new();
}

public sealed class ServiceState
{
    public int StartType { get; set; }        // 2 auto, 3 manual, 4 disabled
    public bool Delayed { get; set; }
    public bool Running { get; set; }
}

public static class RenderModeState
{
    public static readonly string[] ManagedServices = { "WSearch", "fhsvc" };

    public static string SnapshotPath => Path.Combine(AppProfile.Roaming("MacMasterControlPro"), "RenderModeSnapshot.json");

    public static string StartArg(ServiceState s) => s.StartType switch
    {
        2 => s.Delayed ? "delayed-auto" : "auto",
        3 => "demand",
        4 => "disabled",
        _ => "demand",
    };

    /// Comenzi de activare: doar pentru ce era PORNIT. WSearch se dezactiveaza din configuratie doar daca nu era deja.
    public static List<string> ActivationCommands(RenderModeSnapshot s)
    {
        var c = new List<string>();
        if (s.Services.TryGetValue("WSearch", out var ws))
        {
            if (ws.StartType != 4) c.Add("sc config WSearch start= disabled");
            if (ws.Running) c.Add("sc stop WSearch");
        }
        if (s.Services.TryGetValue("fhsvc", out var fh) && fh.Running) c.Add("sc stop fhsvc");
        foreach (var pid in s.Priorities.Keys.OrderBy(x => x))
            c.Add($"Get-Process -Id {pid} -ErrorAction SilentlyContinue | ForEach-Object {{ $_.PriorityClass = 'High' }}");
        return c;
    }

    /// Comenzi de restaurare: exact ce a fost schimbat.
    public static List<string> RestoreCommands(RenderModeSnapshot s)
    {
        var c = new List<string>();
        if (s.Services.TryGetValue("WSearch", out var ws))
        {
            if (ws.StartType != 4) c.Add($"sc config WSearch start= {StartArg(ws)}");
            if (ws.Running) c.Add("sc start WSearch");
        }
        if (s.Services.TryGetValue("fhsvc", out var fh) && fh.Running) c.Add("sc start fhsvc");
        foreach (var (pid, prio) in s.Priorities.OrderBy(x => x.Key))
            c.Add($"Get-Process -Id {pid} -ErrorAction SilentlyContinue | ForEach-Object {{ $_.PriorityClass = '{prio}' }}");
        return c;
    }

    public static List<string> ChangeSummary(RenderModeSnapshot s)
    {
        var l = new List<string>();
        l.Add(!s.Services.TryGetValue("WSearch", out var ws) ? "Windows Search: stare necunoscută, nu se modifică."
            : ws.StartType == 4 && !ws.Running ? "Windows Search: deja oprit, rămâne neschimbat."
            : "Windows Search: se oprește și se dezactivează temporar; la dezactivare revine la modul de pornire de dinainte.");
        l.Add(!s.Services.TryGetValue("fhsvc", out var fh) ? "File History: stare necunoscută, nu se modifică."
            : fh.Running ? "File History: se oprește temporar; se repornește la dezactivare." : "File History: deja oprit, rămâne neschimbat.");
        l.Add(s.Priorities.Count == 0 ? "Prioritate: DaVinci Resolve nu rulează acum." : $"Prioritate: {s.Priorities.Count} proces(e) trec la High; valorile vechi se restaurează.");
        return l;
    }

    public static RenderModeSnapshot ReadCurrent()
    {
        var s = new RenderModeSnapshot();
        foreach (var name in ManagedServices)
        {
            try
            {
                using var k = Registry.LocalMachine.OpenSubKey($@"SYSTEM\CurrentControlSet\Services\{name}");
                if (k?.GetValue("Start") is not int start) continue;
                var delayed = k.GetValue("DelayedAutostart") is int d && d == 1;
                s.Services[name] = new ServiceState { StartType = start, Delayed = delayed, Running = IsRunning(name) };
            }
            catch { /* necunoscut */ }
        }
        try
        {
            foreach (var p in System.Diagnostics.Process.GetProcessesByName("Resolve"))
                try { s.Priorities[p.Id] = p.PriorityClass.ToString(); } catch { }
        }
        catch { }
        return s;
    }

    private static bool IsRunning(string service)
    {
        var o = Shell.Run($"sc query {service}");
        return o.Contains("RUNNING", StringComparison.OrdinalIgnoreCase);
    }

    public static void Save(RenderModeSnapshot s, string? path = null)
    {
        path ??= SnapshotPath;
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var tmp = path + ".tmp";
        File.WriteAllText(tmp, JsonSerializer.Serialize(s));
        File.Move(tmp, path, true);
    }

    public static RenderModeSnapshot? Load(string? path = null)
    {
        try { path ??= SnapshotPath; return File.Exists(path) ? JsonSerializer.Deserialize<RenderModeSnapshot>(File.ReadAllText(path)) : null; }
        catch { return null; }
    }

    public static void Clear(string? path = null) { try { File.Delete(path ?? SnapshotPath); } catch { } }
}
