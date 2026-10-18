namespace MacMasterControlPro.Core.Services;

/// [2026-09-03] Port 1:1 al PrivilegedFileOps.swift (Mac) — cerinta
/// generalizata explicit de Cristi, dupa fix-ul de dezinstalare (o
/// aplicatie detinuta de administrator refuza stergerea normala): "la
/// toate aplicatiile [operatiile de fisiere] sa aiba posibilitatea sa-i
/// ceara sa ruleze ca root/administrator" - nu doar Dezinstalatorul, ci
/// orice modul care sterge fisiere gasite prin scanare (Fisiere mari,
/// Duplicate) trebuie sa treaca automat pe executie privilegiata cand
/// stergerea normala esueaza cu acces refuzat, nu doar sa raporteze eroare.
public static class PrivilegedFileOps
{
    public sealed record BatchResult(IReadOnlyDictionary<string, string?> Results, bool Cancelled);

    /// `null` la succes; altfel un mesaj de eroare gata de afisat in log. Pentru mai multe elemente folosește `DeleteBatch` (o singură autorizare).
    public static string? Delete(string path) => DeleteBatch(new[] { path }).Results[path];

    /// Ștergerea unei selecții: mai întâi tot ce merge cu drepturile utilizatorului (Coșul de reciclare, reversibil, FĂRĂ prompt); doar elementele
    /// rămase formează UN SINGUR plan privilegiat, prezentat și autorizat o singură dată (ștergere permanentă). Refuz/anulare = planul se oprește.
    public static BatchResult DeleteBatch(IEnumerable<string> paths, bool recycleFirst = true)
    {
        var results = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        var need = new List<string>();
        foreach (var path in paths.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            try
            {
                if (!recycleFirst) { }
                else if (File.Exists(path))
                    Microsoft.VisualBasic.FileIO.FileSystem.DeleteFile(path, Microsoft.VisualBasic.FileIO.UIOption.OnlyErrorDialogs, Microsoft.VisualBasic.FileIO.RecycleOption.SendToRecycleBin);
                else if (Directory.Exists(path))
                    Microsoft.VisualBasic.FileIO.FileSystem.DeleteDirectory(path, Microsoft.VisualBasic.FileIO.UIOption.OnlyErrorDialogs, Microsoft.VisualBasic.FileIO.RecycleOption.SendToRecycleBin);
            }
            catch (Exception) { /* cade pe planul privilegiat mai jos, dacă mai există */ }
            if (!File.Exists(path) && !Directory.Exists(path)) { results[path] = null; continue; }
            if (PrivilegedPlan.IsSafeDeletePath(path)) need.Add(path);
            else results[path] = CoreL.X("cale protejată: ștergerea privilegiată este refuzată", "protected path: privileged deletion refused", "ruta protegida: se rechaza la eliminación con privilegios");
        }
        var cancelled = false;
        if (need.Count > 0)
        {
            var steps = need.Select(p => new PrivilegedPlan.Step(p, $"Remove-Item -LiteralPath {PrivilegedPlan.Q(p)} -Force -Recurse -ErrorAction Stop")).ToList();
            var outcome = PrivilegedPlan.Execute(CoreL.X("Ștergere cu drepturi de administrator", "Deletion with administrator rights", "Eliminación con derechos de administrador"), steps);
            cancelled = outcome.Cancelled;
            foreach (var p in need)
            {
                if (outcome.Cancelled) { results[p] = CoreL.X("anulat: nu s-a modificat nimic", "cancelled: nothing was changed", "cancelado: no se modificó nada"); continue; }
                results[p] = (File.Exists(p) || Directory.Exists(p))
                    ? CoreL.X("tot există după ștergerea privilegiată", "still exists after the privileged deletion", "sigue existiendo tras la eliminación con privilegios")
                    : null;
            }
        }
        return new BatchResult(results, cancelled);
    }
}
