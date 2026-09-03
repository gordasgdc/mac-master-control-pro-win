using System.Diagnostics;
using System.Text.Json;

namespace MacMasterControlPro.Core.Services;

/// Port 1:1 al ResolveMediaAuditService.swift (Mac) - EXCLUSIV prin
/// Scripting API-ul oficial DaVinci, niciodata scriere directa in baza de
/// date interna a proiectelor Resolve.
public sealed class ResolveMediaFlag
{
    public required string ClipName { get; init; }
    public required string FilePath { get; init; }
    public required string Reason { get; init; } // "Offline" | "Duplicat"
}

public sealed class ResolveMediaAuditResult
{
    public required string ProjectName { get; init; }
    public required List<ResolveMediaFlag> Flags { get; init; }
    public required int TotalClips { get; init; }
}

public static class ResolveMediaAuditService
{
    // Cai standard Resolve pe Windows (echivalentul scriptAPIPath/scriptLibPath de pe Mac).
    private static readonly string ScriptModulesPath =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            "Blackmagic Design", "DaVinci Resolve", "Support", "Developer", "Scripting", "Modules");
    private const string ScriptLibPath = @"C:\Program Files\Blackmagic Design\DaVinci Resolve\fusionscript.dll";

    private static string? FindPython() =>
        new[] { "python.exe", "python3.exe" }
            .Select(exe => Shell.Run($"where {exe}").Split('\n').FirstOrDefault()?.Trim())
            .FirstOrDefault(p => !string.IsNullOrWhiteSpace(p) && File.Exists(p));

    public static bool IsAvailable => FindPython() != null && Directory.Exists(ScriptModulesPath);

    public static (ResolveMediaAuditResult? result, string? error) ScanCurrentProject()
    {
        var python = FindPython();
        if (python is null || !Directory.Exists(ScriptModulesPath))
            return (null, "scripting_unavailable");

        var script = $$"""
        import sys, os, json
        sys.path.append(r"{{ScriptModulesPath}}")
        try:
            import DaVinciResolveScript as dvr
            resolve = dvr.scriptapp("Resolve")
            if resolve is None:
                print(json.dumps({"error": "no_scripting_access"})); sys.exit(0)
            project = resolve.GetProjectManager().GetCurrentProject()
            if project is None:
                print(json.dumps({"error": "no_project"})); sys.exit(0)
            pool = project.GetMediaPool()
            root = pool.GetRootFolder()
            clips = []
            def walk(folder):
                for clip in folder.GetClipList():
                    path = clip.GetClipProperty("File Path") or ""
                    name = clip.GetClipProperty("Clip Name") or clip.GetName()
                    if path: clips.append({"name": name, "path": path})
                for sub in folder.GetSubFolderList(): walk(sub)
            walk(root)
            print(json.dumps({"project": project.GetName(), "clips": clips}))
        except Exception as e:
            print(json.dumps({"error": str(e)}))
        """;

        var output = RunPython(python, script);
        if (output is null) return (null, "script_failed");

        using var doc = JsonDocument.Parse(output);
        var root2 = doc.RootElement;
        if (root2.TryGetProperty("error", out var errEl)) return (null, errEl.GetString());

        var projectName = root2.GetProperty("project").GetString() ?? "";
        var clipsRaw = root2.GetProperty("clips").EnumerateArray()
            .Select(c => (name: c.GetProperty("name").GetString() ?? "", path: c.GetProperty("path").GetString() ?? ""))
            .ToList();

        var pathCounts = clipsRaw.GroupBy(c => c.path).ToDictionary(g => g.Key, g => g.Count());
        var flags = new List<ResolveMediaFlag>();
        var seenDuplicates = new HashSet<string>();
        foreach (var (name, path) in clipsRaw)
        {
            if (!File.Exists(path))
            {
                flags.Add(new ResolveMediaFlag { ClipName = name, FilePath = path, Reason = "Offline" });
            }
            else if (pathCounts.GetValueOrDefault(path, 0) > 1 && seenDuplicates.Add(path))
            {
                flags.Add(new ResolveMediaFlag { ClipName = name, FilePath = path, Reason = "Duplicat" });
            }
        }
        return (new ResolveMediaAuditResult { ProjectName = projectName, Flags = flags, TotalClips = clipsRaw.Count }, null);
    }

    /// Sterge din Media Pool DOAR clipurile ale caror cai sunt date explicit
    /// (selectie a utilizatorului) - `DeleteClips`, API oficial de scripting.
    public static (int deleted, string? error) DeleteClips(List<string> filePaths)
    {
        var python = FindPython();
        if (python is null || !Directory.Exists(ScriptModulesPath)) return (0, "scripting_unavailable");

        var pathsLiteral = string.Join(", ", filePaths.Select(p => $"r\"{p}\""));
        var script = $$"""
        import sys, json
        sys.path.append(r"{{ScriptModulesPath}}")
        try:
            import DaVinciResolveScript as dvr
            resolve = dvr.scriptapp("Resolve")
            project = resolve.GetProjectManager().GetCurrentProject()
            pool = project.GetMediaPool()
            root = pool.GetRootFolder()
            targets = set([{{pathsLiteral}}])
            to_delete = []
            def walk(folder):
                for clip in folder.GetClipList():
                    if clip.GetClipProperty("File Path") in targets: to_delete.append(clip)
                for sub in folder.GetSubFolderList(): walk(sub)
            walk(root)
            ok = pool.DeleteClips(to_delete) if to_delete else True
            print(json.dumps({"deleted": len(to_delete), "ok": bool(ok)}))
        except Exception as e:
            print(json.dumps({"error": str(e)}))
        """;
        var output = RunPython(python, script);
        if (output is null) return (0, "script_failed");
        using var doc = JsonDocument.Parse(output);
        if (doc.RootElement.TryGetProperty("error", out var errEl)) return (0, errEl.GetString());
        return (doc.RootElement.GetProperty("deleted").GetInt32(), null);
    }

    internal static string? RunPython(string pythonPath, string script, int timeoutSeconds = 20)
    {
        var psi = new ProcessStartInfo
        {
            FileName = pythonPath,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        psi.ArgumentList.Add("-c");
        psi.ArgumentList.Add(script);
        psi.Environment["RESOLVE_SCRIPT_API"] = Path.GetDirectoryName(ScriptModulesPath)!;
        psi.Environment["RESOLVE_SCRIPT_LIB"] = ScriptLibPath;
        psi.Environment["PYTHONPATH"] = ScriptModulesPath;

        // [2026-09-03] FIX REAL, port 1:1 de pe Mac (Shell.swift) — `stderr`
        // era redirectat dar NICIODATA citit; un script Python cu mult
        // output pe stderr putea umple bufferul, blocand scriptul si
        // consumand tot cele `timeoutSeconds` degeaba inainte de kill —
        // eșec silențios ("null"), desi scriptul ar fi mers normal daca
        // cineva ii citea stderr-ul. Citire asincrona pe ambele fluxuri.
        using var process = new Process { StartInfo = psi, EnableRaisingEvents = true };
        var stdout = new System.Text.StringBuilder();
        var sync = new object();
        process.OutputDataReceived += (_, e) => { if (e.Data is not null) lock (sync) stdout.AppendLine(e.Data); };
        process.ErrorDataReceived += (_, __) => { /* drenam, doar ca sa nu se umple bufferul */ };
        if (!process.Start()) return null;
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        if (!process.WaitForExit(timeoutSeconds * 1000))
        {
            try { process.Kill(); } catch { /* best-effort */ }
            return null;
        }
        lock (sync) return process.ExitCode == 0 ? stdout.ToString().Trim() : null;
    }
}
