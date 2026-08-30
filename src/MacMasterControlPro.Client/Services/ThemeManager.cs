using System;
using System.IO;
using Wpf.Ui.Appearance;

namespace MacMasterControlPro.Client.Services;

public enum AppTheme { System, Light, Dark }

public static class ThemeManager
{
    private static readonly string SettingsFilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Master Control Studio Pro", "theme.txt");

    public static AppTheme Current { get; private set; } = Load();

    public static void Apply()
    {
        var resolved = Current == AppTheme.System
            ? (ApplicationThemeManager.GetSystemTheme() == SystemTheme.Light ? ApplicationTheme.Light : ApplicationTheme.Dark)
            : (Current == AppTheme.Light ? ApplicationTheme.Light : ApplicationTheme.Dark);
        ApplicationThemeManager.Apply(resolved);
    }

    public static void Set(AppTheme theme)
    {
        Current = theme;
        Directory.CreateDirectory(Path.GetDirectoryName(SettingsFilePath)!);
        File.WriteAllText(SettingsFilePath, theme.ToString());
        Apply();
    }

    private static AppTheme Load()
    {
        if (File.Exists(SettingsFilePath) && Enum.TryParse<AppTheme>(File.ReadAllText(SettingsFilePath).Trim(), out var saved))
        {
            return saved;
        }
        return AppTheme.System;
    }
}
