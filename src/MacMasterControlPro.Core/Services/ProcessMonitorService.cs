using System.Diagnostics;

namespace MacMasterControlPro.Core.Services;

public sealed record RunningProcessWin(int Pid, string Name, double MemoryMB);

/// Oglinda ProcessMonitorService.swift (Mac). Windows nu expune %CPU
/// instant per-proces fara doua esantioane la interval (spre deosebire de
/// `ps` pe Mac, care da direct o medie) — pentru simplitate si consistenta
/// cu restul UI-ului, aratam RAM (WorkingSet64), sortat descrescator,
/// suficient pentru "ce consuma mult chiar acum".
public static class ProcessMonitorService
{
    public static List<RunningProcessWin> TopProcesses(int limit = 20)
    {
        return Process.GetProcesses()
            .Select(p =>
            {
                try { return new RunningProcessWin(p.Id, p.ProcessName, p.WorkingSet64 / 1024.0 / 1024.0); }
                catch { return null; }
            })
            .Where(p => p is not null)
            .Cast<RunningProcessWin>()
            .OrderByDescending(p => p.MemoryMB)
            .Take(limit)
            .ToList();
    }

    public static void Terminate(int pid, bool force)
    {
        try
        {
            var process = Process.GetProcessById(pid);
            if (force) process.Kill();
            else process.CloseMainWindow();
        }
        catch { /* procesul a dispărut deja sau nu avem acces */ }
    }
}
