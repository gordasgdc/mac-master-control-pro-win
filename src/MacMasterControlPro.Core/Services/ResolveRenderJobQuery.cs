using System.Text.Json;

namespace MacMasterControlPro.Core.Services;

/// Port 1:1 al ResolveRenderJobQuery din RenderNotificationService.swift
/// (Mac) - interogare READ-ONLY a cozii de randare curente din Resolve.
public sealed class ResolveRenderJob
{
    public required string Id { get; init; }
    public required string Status { get; init; }
}

public static class ResolveRenderJobQuery
{
    private static readonly string ScriptModulesPath =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            "Blackmagic Design", "DaVinci Resolve", "Support", "Developer", "Scripting", "Modules");

    public static (List<ResolveRenderJob>? jobs, string? error) FetchJobs()
    {
        // Interogare separata de ScanCurrentProject (alta responsabilitate),
        // acelasi tipar de punte Python.
        var python = FindPython();
        if (python is null || !Directory.Exists(ScriptModulesPath)) return (null, "scripting_unavailable");

        var script = $$"""
        import sys, json
        sys.path.append(r"{{ScriptModulesPath}}")
        try:
            import DaVinciResolveScript as dvr
            resolve = dvr.scriptapp("Resolve")
            project = resolve.GetProjectManager().GetCurrentProject() if resolve else None
            if project is None:
                print(json.dumps({"jobs": []})); sys.exit(0)
            jobs = []
            for job in project.GetRenderJobList() or []:
                job_id = job["JobId"]
                status = project.GetRenderJobStatus(job_id)
                jobs.append({"id": job_id, "status": status.get("JobStatus", "") if isinstance(status, dict) else str(status)})
            print(json.dumps({"jobs": jobs}))
        except Exception as e:
            print(json.dumps({"error": str(e)}))
        """;

        var output = ResolveMediaAuditService.RunPython(python, script, timeoutSeconds: 10);
        if (output is null) return (null, "script_failed");
        using var doc = JsonDocument.Parse(output);
        if (doc.RootElement.TryGetProperty("error", out var errEl)) return (null, errEl.GetString());
        var jobs = doc.RootElement.GetProperty("jobs").EnumerateArray()
            .Select(j => new ResolveRenderJob { Id = j.GetProperty("id").GetString() ?? "", Status = j.GetProperty("status").GetString() ?? "" })
            .ToList();
        return (jobs, null);
    }

    private static string? FindPython() =>
        new[] { "python.exe", "python3.exe" }
            .Select(exe => Shell.Run($"where {exe}").Split('\n').FirstOrDefault()?.Trim())
            .FirstOrDefault(p => !string.IsNullOrWhiteSpace(p) && File.Exists(p));
}
