using System.Runtime.InteropServices;

namespace MacMasterControlPro.Core.Services;

/// Mutare RECUPERABILĂ în Coșul de reciclare (SHFileOperation cu FOF_ALLOWUNDO). Niciodată ștergere definitivă:
/// dacă sistemul refuză, rezultatul e o eroare per element, cu motiv, iar elementul rămâne neatins.
public static class RecycleBinService
{
    public readonly record struct Outcome(bool Moved, string? Error)
    {
        public static Outcome Ok => new(true, null);
        public static Outcome Fail(string why) => new(false, why);
    }

    public static Outcome MoveToRecycleBin(string path)
    {
        if (!File.Exists(path) && !Directory.Exists(path)) return Outcome.Fail("nu mai există");
        var op = new SHFILEOPSTRUCT
        {
            wFunc = 3 /* FO_DELETE */,
            pFrom = path + "\0\0",
            fFlags = 0x0040 /* FOF_ALLOWUNDO */ | 0x0010 /* FOF_NOCONFIRMATION */ | 0x0400 /* FOF_NOERRORUI */ | 0x0004 /* FOF_SILENT */,
        };
        var rc = SHFileOperation(ref op);
        if (rc != 0 || op.fAnyOperationsAborted) return Outcome.Fail(Describe(rc));
        return File.Exists(path) || Directory.Exists(path) ? Outcome.Fail("tot există după mutare") : Outcome.Ok;
    }

    private static string Describe(int rc) => rc switch
    {
        0x20 or 0x21 => "fișier în uz de altă aplicație",
        0x5 or 0x78 => "acces refuzat",
        0x7C => "cale invalidă",
        0x75 => "operațiune anulată",
        _ => $"eroare Windows 0x{rc:X}",
    };

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode, Pack = 8)]
    private struct SHFILEOPSTRUCT
    {
        public IntPtr hwnd; public uint wFunc; [MarshalAs(UnmanagedType.LPWStr)] public string pFrom; [MarshalAs(UnmanagedType.LPWStr)] public string? pTo;
        public ushort fFlags; [MarshalAs(UnmanagedType.Bool)] public bool fAnyOperationsAborted; public IntPtr hNameMappings; [MarshalAs(UnmanagedType.LPWStr)] public string? lpszProgressTitle;
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)] private static extern int SHFileOperation(ref SHFILEOPSTRUCT lpFileOp);
}
