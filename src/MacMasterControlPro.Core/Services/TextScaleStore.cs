namespace MacMasterControlPro.Core.Services;

/// Port 1:1 al `TextScalePreference`/`TextScaleStore` din
/// GDCPluginManagerWin (CLAUDE.md Partea 1, Regula 24) - lipsea din
/// GDCVaultWin, adaugata standard abia dupa ultima actualizare a acestui
/// repo (2026-08-27, inainte de Regula 24 din 2026-08-29).
public enum TextScalePreference
{
    Small,
    Normal,
    Large,
    XLarge,
}

public static class TextScalePreferenceExtensions
{
    public static double ScaleFactor(this TextScalePreference preference) => preference switch
    {
        TextScalePreference.Small => 0.9,
        TextScalePreference.Normal => 1.0,
        TextScalePreference.Large => 1.15,
        TextScalePreference.XLarge => 1.3,
        _ => 1.0,
    };

    public static string DisplayName(this TextScalePreference preference) => preference switch
    {
        TextScalePreference.Small => "Mic",
        TextScalePreference.Normal => "Normal",
        TextScalePreference.Large => "Mare",
        TextScalePreference.XLarge => "Foarte mare",
        _ => "Normal",
    };
}

public static class TextScaleStore
{
    private static string FilePath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Mac Master Control Pro", "text-scale.txt");

    public static TextScalePreference Load()
    {
        try
        {
            if (!File.Exists(FilePath)) return TextScalePreference.Normal;
            var raw = File.ReadAllText(FilePath).Trim();
            return Enum.TryParse<TextScalePreference>(raw, out var value) ? value : TextScalePreference.Normal;
        }
        catch
        {
            return TextScalePreference.Normal;
        }
    }

    public static void Save(TextScalePreference preference)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
            File.WriteAllText(FilePath, preference.ToString());
        }
        catch
        {
            // Nescrierea pe disc nu blocheaza sesiunea curenta.
        }
    }
}
