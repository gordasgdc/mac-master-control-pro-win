namespace MacMasterControlPro.Core.Services;

/// Mod Randare pe Windows: opreste temporar Windows Search si File History si ridica prioritatea Resolve.
/// Starea veche se salveaza INAINTE (RenderModeState); dezactivarea o restaureaza exact, inclusiv dupa o inchidere fortata.
public sealed class RenderModeService
{
    public bool IsActive { get; private set; }
    /// Modul era activ la o sesiune anterioara (inchidere fortata/repornire): sistemul poate fi inca modificat.
    public bool RecoveredFromPreviousSession { get; private set; }

    public RenderModeService()
    {
        if (RenderModeState.Load() is not null) { IsActive = true; RecoveredFromPreviousSession = true; }
    }

    /// Ce ar schimba activarea ACUM, fara sa schimbe nimic.
    public List<string> PreviewChanges() => RenderModeState.ChangeSummary(RenderModeState.ReadCurrent());

    public void Activate(Action<string> log)
    {
        var snapshot = RenderModeState.ReadCurrent();
        var commands = RenderModeState.ActivationCommands(snapshot);
        var op = DiagnosticLog.Begin("render", "activate");
        foreach (var l in RenderModeState.ChangeSummary(snapshot)) log("• " + l);
        if (commands.Count == 0) { log("ℹ Nimic de schimbat: tot ce poate face Modul Randare este deja oprit."); op.End("noop"); return; }
        try { RenderModeState.Save(snapshot); }
        catch (Exception ex) { log("✘ Nu am putut salva starea curentă, nu modific nimic: " + ex.Message); op.End("snapshot-failed", "error"); return; }

        log("$ " + string.Join(" ; ", commands));
        var ok = PrivilegedRunner.Run(commands, line => log("  " + line));
        if (ok)
        {
            IsActive = true;
            log("✔ Mod Randare ACTIV. Starea anterioară e salvată; dezactivarea o restaurează exact.");
            op.End("ok");
        }
        else
        {
            IsActive = true;   // posibil aplicat partial: permite restaurarea
            log("✘ Promptul UAC a fost respins sau comenzile au eșuat. Starea veche rămâne salvată; dezactivează pentru a readuce orice s-a schimbat parțial.");
            op.End("failed", "error");
        }
    }

    public void Deactivate(Action<string> log)
    {
        var snapshot = RenderModeState.Load();
        if (snapshot is null)
        {
            log("ℹ Nu există o stare salvată de restaurat. Nu modific Windows Search sau File History.");
            IsActive = false; RecoveredFromPreviousSession = false; return;
        }
        var commands = RenderModeState.RestoreCommands(snapshot);
        var op = DiagnosticLog.Begin("render", "deactivate");
        if (commands.Count == 0) { RenderModeState.Clear(); IsActive = false; RecoveredFromPreviousSession = false; log("✔ Nimic de restaurat."); op.End("noop"); return; }
        log("$ " + string.Join(" ; ", commands));
        var ok = PrivilegedRunner.Run(commands, line => log("  " + line));
        if (ok)
        {
            RenderModeState.Clear();
            IsActive = false; RecoveredFromPreviousSession = false;
            log("✔ Mod Randare dezactivat: s-a restaurat exact starea de dinainte.");
            op.End("ok");
        }
        else
        {
            log("✘ Restaurarea a eșuat sau UAC a fost respins. Starea salvată a fost păstrată; reîncearcă.");
            op.End("failed", "error");
        }
    }
}
