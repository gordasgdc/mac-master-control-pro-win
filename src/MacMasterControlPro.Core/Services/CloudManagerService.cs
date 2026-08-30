using System.Diagnostics;

namespace MacMasterControlPro.Core.Services;

public sealed class CloudField
{
    public required string Key { get; init; }
    public required string Label { get; init; }
    public bool IsSecure { get; init; }
}

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

/// Manager Universal Multi-Cloud - identic conceptual cu CloudManagerService
/// (Mac), dar monteaza pe o litera de disc (WinFSP), nu un folder Desktop.
public sealed class CloudManagerService
{
    public List<CloudRemote> Remotes { get; private set; } = new();
    public Dictionary<string, string> MountedDriveLetters { get; } = new(); // remoteName -> litera (ex: "X:")

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

    /// Monteaza pe prima litera libera (necesita WinFSP instalat).
    public string? Mount(string remoteName)
    {
        var letter = FirstFreeDriveLetter();
        if (letter is null) return null;
        Process.Start(new ProcessStartInfo("cmd.exe")
        {
            Arguments = $"/c start /min rclone mount {remoteName}: {letter} --vfs-cache-mode off",
            UseShellExecute = false,
            CreateNoWindow = true,
        });
        MountedDriveLetters[remoteName] = letter;
        return letter;
    }

    public void Unmount(string remoteName)
    {
        if (!MountedDriveLetters.TryGetValue(remoteName, out var letter)) return;
        Shell.Run($"rclone rc core/quit --rc-addr 127.0.0.1:5572 2>$null");
        Shell.Run($"net use {letter} /delete /y 2>$null");
        MountedDriveLetters.Remove(remoteName);
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
