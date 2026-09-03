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
    /// `null` la succes; altfel un mesaj de eroare gata de afisat in log.
    /// NOTA: fallback-ul privilegiat sterge PERMANENT (nu la Cosul de
    /// reciclare) - `Remove-Item` elevat intr-un proces separat nu poate
    /// duce fisierul in Cosul de reciclare al sesiunii curente a userului.
    public static string? Delete(string path)
    {
        try
        {
            if (File.Exists(path))
                Microsoft.VisualBasic.FileIO.FileSystem.DeleteFile(
                    path, Microsoft.VisualBasic.FileIO.UIOption.OnlyErrorDialogs,
                    Microsoft.VisualBasic.FileIO.RecycleOption.SendToRecycleBin);
            else if (Directory.Exists(path))
                Microsoft.VisualBasic.FileIO.FileSystem.DeleteDirectory(
                    path, Microsoft.VisualBasic.FileIO.UIOption.OnlyErrorDialogs,
                    Microsoft.VisualBasic.FileIO.RecycleOption.SendToRecycleBin);
            return null;
        }
        catch (Exception)
        {
            // cade pe calea privilegiata mai jos.
        }

        var escaped = path.Replace("'", "''");
        var ok = PrivilegedRunner.Run($"Remove-Item -LiteralPath '{escaped}' -Force -Recurse -ErrorAction Stop");
        if (!ok) return "acces refuzat, chiar și cu drepturi de administrator";
        return (File.Exists(path) || Directory.Exists(path))
            ? "tot există după ștergerea privilegiată"
            : null;
    }
}
