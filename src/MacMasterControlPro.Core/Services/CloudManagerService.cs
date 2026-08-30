using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace MacMasterControlPro.Core.Services;

public sealed class CloudField
{
    public required string Key { get; init; }
    public required string Label { get; init; }
    public bool IsSecure { get; init; }
}

/// Statistici live de transfer (Faza 2), citite din `rclone rc core/stats`.
public sealed record CloudTransferStats(double SpeedBytesPerSec, long BytesTransferred, int ActiveTransfers);

/// O intrare intr-un folder de pe remote (Faza 3 - explorare fara montare).
public sealed record RemoteEntry(string Name, string Path, bool IsDir, long Size);

/// Oglinda CloudProviderType (Mac) — acelasi set de provideri Rclone.
public enum CloudProviderType { GoogleDrive, Dropbox, OneDrive, PCloud, Degoo, Mega, S3, WebDav, Sftp, Ftp }

public static class CloudProviderTypeExtensions
{
    public static string RcloneType(this CloudProviderType type) => type switch
    {
        CloudProviderType.GoogleDrive => "drive",
        CloudProviderType.Dropbox => "dropbox",
        CloudProviderType.OneDrive => "onedrive",
        CloudProviderType.PCloud => "pcloud",
        CloudProviderType.Degoo => "degoo",
        CloudProviderType.Mega => "mega",
        CloudProviderType.S3 => "s3",
        CloudProviderType.WebDav => "webdav",
        CloudProviderType.Sftp => "sftp",
        CloudProviderType.Ftp => "ftp",
        _ => "drive",
    };

    public static string Label(this CloudProviderType type) => type switch
    {
        CloudProviderType.GoogleDrive => "Google Drive",
        CloudProviderType.Dropbox => "Dropbox",
        CloudProviderType.OneDrive => "OneDrive",
        CloudProviderType.PCloud => "pCloud",
        CloudProviderType.Degoo => "Degoo",
        CloudProviderType.Mega => "Mega",
        CloudProviderType.S3 => "AWS S3 / compatibil",
        CloudProviderType.WebDav => "WebDAV",
        CloudProviderType.Sftp => "SFTP / NAS",
        CloudProviderType.Ftp => "FTP",
        _ => type.ToString(),
    };

    public static bool IsOAuth(this CloudProviderType type) =>
        type is CloudProviderType.GoogleDrive or CloudProviderType.Dropbox or CloudProviderType.OneDrive or CloudProviderType.PCloud;

    public static List<CloudField> Fields(this CloudProviderType type) => type switch
    {
        CloudProviderType.GoogleDrive or CloudProviderType.Dropbox or CloudProviderType.OneDrive or CloudProviderType.PCloud => new(),
        CloudProviderType.Degoo or CloudProviderType.Mega => new()
        {
            new CloudField { Key = "user", Label = "Email" },
            new CloudField { Key = "pass", Label = "Parolă", IsSecure = true },
        },
        CloudProviderType.S3 => new()
        {
            new CloudField { Key = "provider", Label = "Provider (ex: AWS, Wasabi, Minio)" },
            new CloudField { Key = "access_key_id", Label = "Access Key ID" },
            new CloudField { Key = "secret_access_key", Label = "Secret Access Key", IsSecure = true },
            new CloudField { Key = "region", Label = "Regiune (ex: eu-central-1)" },
        },
        CloudProviderType.WebDav => new()
        {
            new CloudField { Key = "url", Label = "URL server" },
            new CloudField { Key = "vendor", Label = "Vendor (nextcloud/owncloud/other)" },
            new CloudField { Key = "user", Label = "Utilizator" },
            new CloudField { Key = "pass", Label = "Parolă", IsSecure = true },
        },
        CloudProviderType.Sftp => new()
        {
            new CloudField { Key = "host", Label = "IP / Host NAS" },
            new CloudField { Key = "user", Label = "Utilizator" },
            new CloudField { Key = "pass", Label = "Parolă", IsSecure = true },
            new CloudField { Key = "port", Label = "Port (implicit 22)" },
        },
        CloudProviderType.Ftp => new()
        {
            new CloudField { Key = "host", Label = "Host" },
            new CloudField { Key = "user", Label = "Utilizator" },
            new CloudField { Key = "pass", Label = "Parolă", IsSecure = true },
        },
        _ => new(),
    };
}

public sealed class CloudRemote
{
    public required string Name { get; init; }
    public required string Type { get; init; }
}

/// Setare persistata: folder custom (posibil pe disc extern) unde se
/// monteaza remote-urile, in loc de o litera de disc noua - cerinta
/// explicita 2026-08-30 (Cristi: SSD-uri interne mici, se lucreaza de pe
/// discuri externe Thunderbolt/USB-C). `null` = comportament vechi
/// (prima litera libera).
public static class CloudMountSettings
{
    private static string FilePath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "MacMasterControlPro", "cloud-mount-settings.json");

    public static string? CustomMountFolder
    {
        get
        {
            try
            {
                if (!File.Exists(FilePath)) return null;
                var json = JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllBytes(FilePath));
                return json?.GetValueOrDefault("customMountFolder");
            }
            catch { return null; }
        }
        set
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
                var json = new Dictionary<string, string?> { ["customMountFolder"] = value };
                File.WriteAllBytes(FilePath, JsonSerializer.SerializeToUtf8Bytes(json));
            }
            catch { /* nescrierea nu trebuie sa blocheze sesiunea curenta */ }
        }
    }
}

/// Manager Universal Multi-Cloud - identic conceptual cu CloudManagerService
/// (Mac). Monteaza fie pe o litera de disc noua (WinFSP, implicit), fie
/// intr-un folder ales de user (posibil pe disc extern) - vezi
/// `CloudMountSettings`.
public sealed class CloudManagerService
{
    /// Port RC unic per montare, NU un port fix comun - bug real gasit
    /// 2026-08-30: montarea nu pornea deloc cu `--rc` (deci `rclone rc
    /// core/quit` din Unmount nu avea la ce sa se conecteze, demontarea
    /// reala se baza doar pe `net use /delete`, care nu opreste procesul
    /// WinFsp de dedesubt), iar un port comun ar fi facut ca a doua
    /// montare simultana sa o demonteze accidental pe prima.
    private const int RcBasePort = 5572;
    private readonly Dictionary<string, int> _rcPorts = new();

    public List<CloudRemote> Remotes { get; private set; } = new();
    /// remoteName -> litera ("X:") SAU folder complet, dupa cum e configurat.
    public Dictionary<string, string> MountedDriveLetters { get; } = new();

    public void RefreshRemotes()
    {
        var raw = Shell.Run("rclone listremotes --long 2>$null");
        Remotes = raw.Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(line =>
            {
                var parts = line.Split(':', 2);
                return parts.Length == 2 ? new CloudRemote { Name = parts[0].Trim(), Type = parts[1].Trim() } : null;
            })
            .Where(r => r is not null)
            .Select(r => r!)
            .ToList();
    }

    /// Provideri OAuth - rclone deschide singur browser-ul pentru autorizare.
    public (bool success, string output) CreateRemote(string name, CloudProviderType type, Dictionary<string, string> values)
    {
        var args = new List<string> { "config", "create", name, type.RcloneType() };
        foreach (var field in type.Fields())
        {
            if (!values.TryGetValue(field.Key, out var value) || string.IsNullOrEmpty(value)) continue;
            if (field.IsSecure)
            {
                var obscured = Shell.Run($"rclone obscure \"{value.Replace("\"", "\\\"")}\"");
                args.Add($"{field.Key}={obscured}");
            }
            else
            {
                args.Add($"{field.Key}={value}");
            }
        }

        var psi = new ProcessStartInfo("rclone")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        foreach (var arg in args) psi.ArgumentList.Add(arg);

        try
        {
            using var process = Process.Start(psi);
            if (process is null) return (false, "Nu s-a putut porni rclone.");
            var output = process.StandardOutput.ReadToEnd() + process.StandardError.ReadToEnd();
            process.WaitForExit();
            RefreshRemotes();
            return (process.ExitCode == 0, output);
        }
        catch (Exception ex)
        {
            return (false, ex.Message);
        }
    }

    public void DeleteRemote(string name)
    {
        Shell.Run($"rclone config delete \"{name}\"");
        RefreshRemotes();
    }

    /// Monteaza fie pe un folder custom (CloudMountSettings, posibil disc
    /// extern), fie pe prima litera libera (necesita WinFSP instalat) -
    /// `log` primeste linia de comanda + orice eroare (panou Terminal Live).
    public string? Mount(string remoteName, Action<string>? log = null)
    {
        var customFolder = CloudMountSettings.CustomMountFolder;
        string target;
        if (!string.IsNullOrWhiteSpace(customFolder))
        {
            if (!Directory.Exists(customFolder))
            {
                log?.Invoke($"⚠ Folderul de mount configurat ({customFolder}) nu există (disc extern deconectat?) — folosesc o literă de disc în loc.");
                target = FirstFreeDriveLetter() ?? "";
                if (target == "") { log?.Invoke("✗ Nicio literă de disc liberă."); return null; }
            }
            else
            {
                target = Path.Combine(customFolder, $"Cloud_{remoteName}");
                Directory.CreateDirectory(target);
            }
        }
        else
        {
            target = FirstFreeDriveLetter() ?? "";
            if (target == "") { log?.Invoke("✗ Nicio literă de disc liberă."); return null; }
        }

        var port = RcBasePort + _rcPorts.Count;
        _rcPorts[remoteName] = port;
        log?.Invoke($"$ rclone mount {remoteName}: {target} --rc-addr 127.0.0.1:{port}");
        Process.Start(new ProcessStartInfo("cmd.exe")
        {
            Arguments = $"/c start /min rclone mount {remoteName}: \"{target}\" --vfs-cache-mode off --rc --rc-addr 127.0.0.1:{port} --rc-no-auth",
            UseShellExecute = false,
            CreateNoWindow = true,
        });
        MountedDriveLetters[remoteName] = target;
        return target;
    }

    public void Unmount(string remoteName, Action<string>? log = null)
    {
        if (!MountedDriveLetters.TryGetValue(remoteName, out var target)) return;
        if (_rcPorts.TryGetValue(remoteName, out var port))
        {
            log?.Invoke($"$ rclone rc core/quit --rc-addr 127.0.0.1:{port}");
            Shell.Run($"rclone rc core/quit --rc-addr 127.0.0.1:{port} 2>$null");
            _rcPorts.Remove(remoteName);
        }
        // Fallback pentru montari vechi pe litera (fara --rc) - `net use`
        // functioneaza doar pe litere, niciodata pe un folder custom.
        if (target.Length == 2 && target[1] == ':')
        {
            Shell.Run($"net use {target} /delete /y 2>$null");
        }
        MountedDriveLetters.Remove(remoteName);
        log?.Invoke($"✔ {remoteName}: demontat.");
    }

    // MARK: - Faza 2: Statistici live de transfer

    /// Citeste `core/stats` din API-ul local al montarii - `null` daca
    /// remote-ul nu e montat.
    public CloudTransferStats? FetchStats(string remoteName)
    {
        if (!_rcPorts.TryGetValue(remoteName, out var port)) return null;
        var output = Shell.Run($"rclone rc core/stats --rc-addr 127.0.0.1:{port} 2>$null");
        try
        {
            using var doc = JsonDocument.Parse(output);
            var root = doc.RootElement;
            var speed = root.TryGetProperty("speed", out var s) ? s.GetDouble() : 0;
            var bytes = root.TryGetProperty("bytes", out var b) ? b.GetInt64() : 0;
            var transfers = root.TryGetProperty("transfers", out var t) ? t.GetInt32() : 0;
            return new CloudTransferStats(speed, bytes, transfers);
        }
        catch { return null; }
    }

    // MARK: - Faza 3: Explorare remote fara montare

    /// Listeaza un folder de pe remote prin `rclone lsjson`, FARA sa
    /// monteze nimic.
    public List<RemoteEntry> ListRemoteFolder(string remoteName, string path)
    {
        var target = string.IsNullOrEmpty(path) ? $"{remoteName}:" : $"{remoteName}:{path}";
        var output = Shell.Run($"rclone lsjson \"{target}\" 2>$null");
        try
        {
            using var doc = JsonDocument.Parse(output);
            var results = new List<RemoteEntry>();
            foreach (var item in doc.RootElement.EnumerateArray())
            {
                var name = item.GetProperty("Name").GetString() ?? "";
                var isDir = item.TryGetProperty("IsDir", out var d) && d.GetBoolean();
                var size = item.TryGetProperty("Size", out var sz) ? sz.GetInt64() : 0;
                var childPath = string.IsNullOrEmpty(path) ? name : $"{path}/{name}";
                results.Add(new RemoteEntry(name, childPath, isDir, size));
            }
            return results.OrderByDescending(r => r.IsDir).ThenBy(r => r.Name, StringComparer.OrdinalIgnoreCase).ToList();
        }
        catch { return []; }
    }

    private static string? FirstFreeDriveLetter()
    {
        var used = DriveInfo.GetDrives().Select(d => d.Name[0]).ToHashSet();
        for (var c = 'E'; c <= 'Z'; c++)
        {
            if (!used.Contains(c)) return $"{c}:";
        }
        return null;
    }
}
