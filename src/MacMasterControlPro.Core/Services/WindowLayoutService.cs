using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;

namespace MacMasterControlPro.Core.Services;

/// Port 1:1 al WindowLayoutService.swift (Mac) - salveaza/restaureaza
/// pozitiile ferestrelor unei aplicatii, prin Win32 API (`EnumWindows`/
/// `GetWindowRect`/`MoveWindow`) - echivalentul Windows al Accessibility
/// API de pe Mac. Nu necesita nicio permisiune speciala (spre deosebire de
/// Mac) - un proces poate repozitiona ferestrele altui proces al ACELUIASI
/// utilizator, fara UAC.
public sealed class SavedWindowFrame
{
    public string Title { get; set; } = "";
    public int TitleIndex { get; set; }
    public int X { get; set; }
    public int Y { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
}

public sealed class WindowLayoutProfile
{
    public string Name { get; set; } = "";
    public string ProcessName { get; set; } = "";
    public List<SavedWindowFrame> Frames { get; set; } = new();
    public DateTime SavedAt { get; set; }
}

public static class WindowLayoutService
{
    [DllImport("user32.dll")] private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);
    private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);
    [DllImport("user32.dll")] private static extern int GetWindowThreadProcessId(IntPtr hWnd, out int processId);
    [DllImport("user32.dll", CharSet = CharSet.Auto)] private static extern int GetWindowTextLength(IntPtr hWnd);
    [DllImport("user32.dll", CharSet = CharSet.Auto)] private static extern int GetWindowText(IntPtr hWnd, StringBuilder text, int count);
    [DllImport("user32.dll")] private static extern bool IsWindowVisible(IntPtr hWnd);
    [DllImport("user32.dll")] private static extern bool GetWindowRect(IntPtr hWnd, out Rect rect);
    [DllImport("user32.dll")] private static extern bool MoveWindow(IntPtr hWnd, int x, int y, int width, int height, bool repaint);

    [StructLayout(LayoutKind.Sequential)]
    private struct Rect { public int Left, Top, Right, Bottom; }

    private static readonly string StorePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "MacMasterControlPro", "WindowLayoutProfiles.json");

    public static List<(string name, string processName, int pid)> RunningApps() =>
        Process.GetProcesses()
            .Where(p => p.MainWindowHandle != IntPtr.Zero && !string.IsNullOrWhiteSpace(p.MainWindowTitle))
            .Select(p => (p.MainWindowTitle, p.ProcessName, p.Id))
            .OrderBy(t => t.MainWindowTitle)
            .ToList();

    private static List<(IntPtr handle, string title)> WindowsForProcess(int pid)
    {
        var results = new List<(IntPtr, string)>();
        EnumWindows((hWnd, _) =>
        {
            GetWindowThreadProcessId(hWnd, out var windowPid);
            if (windowPid != pid || !IsWindowVisible(hWnd)) return true;
            var length = GetWindowTextLength(hWnd);
            if (length == 0) return true;
            var sb = new StringBuilder(length + 1);
            GetWindowText(hWnd, sb, sb.Capacity);
            results.Add((hWnd, sb.ToString()));
            return true;
        }, IntPtr.Zero);
        return results;
    }

    public static bool SaveLayout(string name, string processName, int pid)
    {
        var windows = WindowsForProcess(pid);
        if (windows.Count == 0) return false;

        var titleCounts = new Dictionary<string, int>();
        var frames = new List<SavedWindowFrame>();
        foreach (var (handle, title) in windows)
        {
            var index = titleCounts.GetValueOrDefault(title, 0);
            titleCounts[title] = index + 1;
            if (!GetWindowRect(handle, out var rect)) continue;
            frames.Add(new SavedWindowFrame
            {
                Title = title, TitleIndex = index,
                X = rect.Left, Y = rect.Top,
                Width = rect.Right - rect.Left, Height = rect.Bottom - rect.Top,
            });
        }
        if (frames.Count == 0) return false;

        var profiles = AllProfiles().Where(p => p.Name != name).ToList();
        profiles.Add(new WindowLayoutProfile { Name = name, ProcessName = processName, Frames = frames, SavedAt = DateTime.Now });
        Persist(profiles);
        return true;
    }

    public static int RestoreLayout(WindowLayoutProfile profile, int pid)
    {
        var windows = WindowsForProcess(pid);
        var titleCounts = new Dictionary<string, int>();
        var restored = 0;
        foreach (var (handle, title) in windows)
        {
            var index = titleCounts.GetValueOrDefault(title, 0);
            titleCounts[title] = index + 1;
            var frame = profile.Frames.FirstOrDefault(f => f.Title == title && f.TitleIndex == index);
            if (frame is null) continue;
            MoveWindow(handle, frame.X, frame.Y, frame.Width, frame.Height, true);
            restored++;
        }
        return restored;
    }

    public static List<WindowLayoutProfile> AllProfiles()
    {
        if (!File.Exists(StorePath)) return new();
        try { return JsonSerializer.Deserialize<List<WindowLayoutProfile>>(File.ReadAllText(StorePath))?.OrderByDescending(p => p.SavedAt).ToList() ?? new(); }
        catch { return new(); }
    }

    public static void DeleteProfile(string name) => Persist(AllProfiles().Where(p => p.Name != name).ToList());

    private static void Persist(List<WindowLayoutProfile> profiles)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(StorePath)!);
        File.WriteAllText(StorePath, JsonSerializer.Serialize(profiles));
    }
}
