namespace MacMasterControlPro.Core.Services;

/// Profil optional in sidebar (Regula 12) - Nume/Email persistate local.
public static class UserProfileStore
{
    private static string FilePath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Mac Master Control Pro", "profile.txt");

    public static string Name
    {
        get => Load().name;
        set => Save(value, Email);
    }

    public static string Email
    {
        get => Load().email;
        set => Save(Name, value);
    }

    private static (string name, string email) Load()
    {
        try
        {
            if (!File.Exists(FilePath)) return ("", "");
            var lines = File.ReadAllLines(FilePath);
            return (lines.Length > 0 ? lines[0] : "", lines.Length > 1 ? lines[1] : "");
        }
        catch { return ("", ""); }
    }

    private static void Save(string name, string email)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
            File.WriteAllLines(FilePath, new[] { name, email });
        }
        catch { /* nescriere nu blocheaza UI */ }
    }
}
