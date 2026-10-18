using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media.Imaging;

namespace MacMasterControlPro.Client.Services;

/// Iconurile NATIVE ale unitatilor, folderelor si aplicatiilor, de la Shell (IShellItemImageFactory):
/// includ iconurile personalizate (desktop.ini, autorun). Se incarca pe un thread de fundal, se rein in cache
/// si se ofera ca BitmapSource inghetat; interfata nu asteapta niciodata dupa disc.
public static class ShellIconCache
{
    private static readonly ConcurrentDictionary<(string path, int size), BitmapSource> Cache = new();

    public static BitmapSource? Cached(string path, int size) => Cache.TryGetValue((path, size), out var b) ? b : null;

    public static Task<BitmapSource?> GetAsync(string path, int size = 48) =>
        Cached(path, size) is { } hit ? Task.FromResult<BitmapSource?>(hit) : Task.Run(() =>
        {
            var bmp = Load(path, size);
            if (bmp is not null) Cache[(path, size)] = bmp;
            return bmp;
        });

    private static BitmapSource? Load(string path, int size)
    {
        IntPtr hbm = IntPtr.Zero;
        try
        {
            var iid = typeof(IShellItemImageFactory).GUID;
            if (SHCreateItemFromParsingName(path, IntPtr.Zero, ref iid, out var factory) != 0 || factory is null) return null;
            var hr = factory.GetImage(new SIZE { cx = size, cy = size }, SIIGBF.BIGGERSIZEOK, out hbm);
            if (hr != 0 || hbm == IntPtr.Zero) return null;
            var src = Imaging.CreateBitmapSourceFromHBitmap(hbm, IntPtr.Zero, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
            src.Freeze();   // utilizabil pe orice thread
            return src;
        }
        catch { return null; }
        finally { if (hbm != IntPtr.Zero) DeleteObject(hbm); }
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode, PreserveSig = true)]
    private static extern int SHCreateItemFromParsingName(string path, IntPtr pbc, ref Guid riid, [MarshalAs(UnmanagedType.Interface)] out IShellItemImageFactory ppv);
    [DllImport("gdi32.dll")] private static extern bool DeleteObject(IntPtr hObject);

    [StructLayout(LayoutKind.Sequential)] private struct SIZE { public int cx, cy; }
    [Flags] private enum SIIGBF { RESIZETOFIT = 0, BIGGERSIZEOK = 1, MEMORYONLY = 2, ICONONLY = 4, THUMBNAILONLY = 8 }

    [ComImport, Guid("bcc18b79-ba16-442f-80c4-8a59c30c463b"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IShellItemImageFactory
    {
        [PreserveSig] int GetImage(SIZE size, SIIGBF flags, out IntPtr phbm);
    }
}
