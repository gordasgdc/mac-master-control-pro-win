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
        if (commands.Count == 0) { log(CoreL.X("ℹ Nimic de schimbat: tot ce poate face Modul Randare este deja oprit.", "ℹ Nothing to change: everything Render Mode can do is already off.", "ℹ Nada que cambiar: todo lo que puede hacer el modo de renderizado ya está desactivado.")); op.End("noop"); return; }
        try { RenderModeState.Save(snapshot); }
        catch (Exception ex) { log(CoreL.X("✘ Nu am putut salva starea curentă, nu modific nimic: ", "✘ Could not save the current state, changing nothing: ", "✘ No se pudo guardar el estado actual, no se modifica nada: ") + ex.Message); op.End("snapshot-failed", "error"); return; }

        log("$ " + string.Join(" ; ", commands));
        var ok = PrivilegedRunner.Run(commands, line => log("  " + line));
        if (ok)
        {
            IsActive = true;
            log(CoreL.X("✔ Mod Randare ACTIV. Starea anterioară e salvată; dezactivarea o restaurează exact.", "✔ Render Mode ON. The previous state is saved; turning it off restores it exactly.", "✔ Modo de renderizado ACTIVO. El estado anterior está guardado; al desactivarlo se restaura exactamente."));
            op.End("ok");
        }
        else
        {
            IsActive = true;   // posibil aplicat partial: permite restaurarea
            log(CoreL.X("✘ Promptul UAC a fost respins sau comenzile au eșuat. Starea veche rămâne salvată; dezactivează pentru a readuce orice s-a schimbat parțial.", "✘ The UAC prompt was declined or the commands failed. The old state stays saved; turn Render Mode off to bring back anything that changed partially.", "✘ Se rechazó el aviso de UAC o los comandos fallaron. El estado anterior sigue guardado; desactiva el modo para recuperar lo que cambió parcialmente."));
            op.End("failed", "error");
        }
    }

    public void Deactivate(Action<string> log)
    {
        var snapshot = RenderModeState.Load();
        if (snapshot is null)
        {
            log(CoreL.X("ℹ Nu există o stare salvată de restaurat. Nu modific Windows Search sau File History.", "ℹ There is no saved state to restore. Windows Search and File History are left untouched.", "ℹ No hay ningún estado guardado que restaurar. No se modifican Windows Search ni el Historial de archivos."));
            IsActive = false; RecoveredFromPreviousSession = false; return;
        }
        var commands = RenderModeState.RestoreCommands(snapshot);
        var op = DiagnosticLog.Begin("render", "deactivate");
        if (commands.Count == 0) { RenderModeState.Clear(); IsActive = false; RecoveredFromPreviousSession = false; log(CoreL.X("✔ Nimic de restaurat.", "✔ Nothing to restore.", "✔ Nada que restaurar.")); op.End("noop"); return; }
        log("$ " + string.Join(" ; ", commands));
        var ok = PrivilegedRunner.Run(commands, line => log("  " + line));
        if (ok)
        {
            RenderModeState.Clear();
            IsActive = false; RecoveredFromPreviousSession = false;
            log(CoreL.X("✔ Mod Randare dezactivat: s-a restaurat exact starea de dinainte.", "✔ Render Mode off: the exact previous state was restored.", "✔ Modo de renderizado desactivado: se restauró exactamente el estado anterior."));
            op.End("ok");
        }
        else
        {
            log(CoreL.X("✘ Restaurarea a eșuat sau UAC a fost respins. Starea salvată a fost păstrată; reîncearcă.", "✘ Restoring failed or UAC was declined. The saved state was kept; try again.", "✘ Falló la restauración o se rechazó UAC. Se conservó el estado guardado; inténtalo de nuevo."));
            op.End("failed", "error");
        }
    }
}
