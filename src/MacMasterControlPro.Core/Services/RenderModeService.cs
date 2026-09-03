namespace MacMasterControlPro.Core.Services;

/// "Mod Randare" (2026-08-31, port 1:1 al RenderModeService.swift - Mac) —
/// elimina cele mai frecvente surse de contentie I/O in timpul unui
/// export/randare lung: indexarea Windows Search, serviciul de File
/// History, si prioritatea implicita a procesului DaVinci Resolve.exe.
/// Revine automat la starea normala la dezactivare.
public sealed class RenderModeService
{
    public bool IsActive { get; private set; }

    public void Activate(Action<string> log)
    {
        var commands = new List<string>
        {
            "sc config WSearch start= disabled",
            "sc stop WSearch",
            "sc stop fhsvc",
        };
        var resolveCommand = "Get-Process -Name 'Resolve' -ErrorAction SilentlyContinue | ForEach-Object { $_.PriorityClass = 'High' }";
        commands.Add(resolveCommand);

        log("$ " + string.Join(" ; ", commands));
        var ok = PrivilegedRunner.Run(commands, line => log("  " + line));
        if (ok)
        {
            log("✔ Indexare Windows Search oprită.");
            log("✔ File History pus pe pauză.");
            log("✔ DaVinci Resolve (dacă rulează) ridicat la prioritate mare.");
            IsActive = true;
            log("✔ Mod Randare ACTIV.");
        }
        else
        {
            log("✘ Promptul UAC a fost respins — nicio comandă nu a rulat.");
        }
    }

    public void Deactivate(Action<string> log)
    {
        var commands = new List<string>
        {
            "sc config WSearch start= delayed-auto",
            "sc start WSearch",
            "sc start fhsvc",
        };
        log("$ " + string.Join(" ; ", commands));
        var ok = PrivilegedRunner.Run(commands, line => log("  " + line));
        if (ok)
        {
            log("✔ Windows Search reactivat.");
            log("✔ File History reactivat.");
            IsActive = false;
            log("✔ Mod Randare dezactivat — revenit la normal.");
        }
        else
        {
            log("✘ Promptul UAC a fost respins — nicio comandă nu a rulat.");
        }
    }
}
