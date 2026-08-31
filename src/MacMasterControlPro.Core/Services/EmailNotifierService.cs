using System.Net;
using System.Net.Mail;
using System.Text.Json;

namespace MacMasterControlPro.Core.Services;

/// Port 1:1 al EmailNotifierService.swift (Mac) - notificare automata pe
/// email (WhatsApp NU poate trimite fara click manual, doar email e cu
/// adevarat automat catre telefon). Stocare LOCALA, in clar (JSON in
/// AppData) - recomanda explicit in UI o "parola de aplicatie", nu parola
/// reala a contului.
public sealed class EmailNotifierSettings
{
    public bool Enabled { get; set; }
    public string SmtpHost { get; set; } = "smtp.gmail.com";
    public int SmtpPort { get; set; } = 587;
    public string Username { get; set; } = "";
    public string AppPassword { get; set; } = "";
    public string Recipient { get; set; } = "";
}

public static class EmailNotifierService
{
    private static readonly string StorePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "MacMasterControlPro", "EmailNotifierSettings.json");

    public static EmailNotifierSettings Settings
    {
        get
        {
            if (!File.Exists(StorePath)) return new EmailNotifierSettings();
            try { return JsonSerializer.Deserialize<EmailNotifierSettings>(File.ReadAllText(StorePath)) ?? new(); }
            catch { return new(); }
        }
        set
        {
            Directory.CreateDirectory(Path.GetDirectoryName(StorePath)!);
            File.WriteAllText(StorePath, JsonSerializer.Serialize(value));
        }
    }

    public static (bool ok, string? error) Send(string subject, string body)
    {
        var s = Settings;
        if (!s.Enabled) return (false, "Notificarea pe email e dezactivată.");
        if (string.IsNullOrWhiteSpace(s.Username) || string.IsNullOrWhiteSpace(s.AppPassword) || string.IsNullOrWhiteSpace(s.Recipient))
            return (false, "Completează email, parolă de aplicație și destinatar în Setări.");

        try
        {
#pragma warning disable SYSLIB0014 // SmtpClient e marcat obsolet in .NET, dar functional pentru acest caz simplu, sincron - fara dependinta noua (MailKit etc.)
            using var client = new SmtpClient(s.SmtpHost, s.SmtpPort)
            {
                EnableSsl = true,
                Credentials = new NetworkCredential(s.Username, s.AppPassword),
            };
#pragma warning restore SYSLIB0014
            using var message = new MailMessage(s.Username, s.Recipient, subject, body);
            client.Send(message);
            return (true, null);
        }
        catch (Exception ex)
        {
            return (false, ex.Message);
        }
    }
}
