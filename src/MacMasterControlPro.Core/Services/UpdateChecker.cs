using System.ComponentModel;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json;

namespace MacMasterControlPro.Core.Services;

/// Compara versiunea rulata cu ultimul tag de pe GitHub Releases
/// (gdc-vault-win) - GDC Vault nu are un update.json separat (spre
/// deosebire de GDCPluginManager), deci foloseste direct API-ul GitHub,
/// port 1:1 al UpdateChecker.swift de pe gdc-vault-mac.
///
/// BUG FIX 2026-08-27 (raportat de Cristi, pe Mac ȘI Windows: "clientul
/// niciodata nu trebuie sa vada GitHub"): un fix anterior din aceeasi zi
/// doar inlocuise link-ul paginii cu link-ul DIRECT al asset-ului - tot
/// deschidea browserul, doar descarca fisierul in loc sa arate pagina.
/// Nu era suficient. Fix REAL, vezi SelfUpdater.cs: descarca installer-ul
/// cu HttpClient si il lanseaza direct (Process.Start), fara sa treaca
/// deloc prin browser.
public sealed class UpdateChecker : INotifyPropertyChanged
{
    public static readonly UpdateChecker Shared = new();

    private static readonly Uri LatestReleaseApiUrl =
        new("https://api.github.com/repos/gordasgdc/mac-master-control-pro-win/releases/latest");
    public static readonly Uri ReleasesPageUrl =
        new("https://github.com/gordasgdc/mac-master-control-pro-win/releases/latest");

    /// BUG FIX 2026-08-27 (raportat de Cristi: butonul "Descarcă" duce pe
    /// pagina GitHub, nu descarcă direct): link direct spre asset-ul
    /// installer-ului, `releases/latest/download/<nume-stabil>` (vezi
    /// CLAUDE.md Regula 13/17) - deschiderea lui in browser DECLANSEAZA
    /// descarcarea fisierului, spre deosebire de ReleasesPageUrl (pagina
    /// web a release-ului, cu asset-urile listate, dar niciunul descarcat
    /// automat).
    public static readonly Uri DirectDownloadUrl =
        new("https://github.com/gordasgdc/mac-master-control-pro-win/releases/latest/download/MacMasterControlProSetup.exe");

    private readonly HttpClient _http = new();
    private const string DismissedVersionKey = "mmc_dismissed_update_version";

    /// Non-null cat timp exista o versiune mai noua, NEinchisa inca de
    /// utilizator pentru versiunea respectiva.
    public string? AvailableVersion { get; private set; }

    public event PropertyChangedEventHandler? PropertyChanged;

    public static string CurrentVersion =>
        Assembly.GetEntryAssembly()?.GetName().Version?.ToString(3) ?? "0";

    public UpdateChecker()
    {
        _http.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("MacMasterControlPro", CurrentVersion));
        _http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        _http.Timeout = TimeSpan.FromSeconds(10);
    }

    /// Verifica fresh, IGNORAND orice dismissal anterior - folosita direct
    /// de verificarea manuala ("Caută actualizări"), care trebuie mereu
    /// sa arate rezultatul real, chiar daca userul a inchis deja pop-up-ul
    /// pentru aceeasi versiune la o lansare anterioara. Vezi
    /// WasDismissed(string) pentru filtrul aplicat DOAR la verificarea
    /// automata silentioasa de la lansare.
    public async Task CheckAsync()
    {
        string? latest;
        try
        {
            using var response = await _http.GetAsync(LatestReleaseApiUrl);
            if (!response.IsSuccessStatusCode) return;
            var data = await response.Content.ReadAsByteArrayAsync();
            using var doc = JsonDocument.Parse(data);
            var tag = doc.RootElement.GetProperty("tag_name").GetString();
            latest = tag?.StartsWith("v") == true ? tag[1..] : tag;
        }
        catch
        {
            return;
        }
        AvailableVersion = (!string.IsNullOrEmpty(latest) && IsNewer(latest, CurrentVersion)) ? latest : null;
        Raise(nameof(AvailableVersion));
    }

    public bool WasDismissed(string version) => ReadDismissedVersion() == version;

    public void Dismiss()
    {
        if (AvailableVersion is null) return;
        WriteDismissedVersion(AvailableVersion);
        AvailableVersion = null;
        Raise(nameof(AvailableVersion));
    }

    private static string DismissedVersionFilePath =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "MacMasterControlPro", "dismissed-update-version.txt");

    private static string? ReadDismissedVersion()
    {
        try { return File.Exists(DismissedVersionFilePath) ? File.ReadAllText(DismissedVersionFilePath).Trim() : null; }
        catch { return null; }
    }

    private static void WriteDismissedVersion(string version)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(DismissedVersionFilePath)!);
            File.WriteAllText(DismissedVersionFilePath, version);
        }
        catch { /* nescriere nu trebuie sa blocheze UI-ul */ }
    }

    private static bool IsNewer(string a, string b)
    {
        var partsA = a.Split('.').Select(s => int.TryParse(s, out var n) ? n : 0).ToArray();
        var partsB = b.Split('.').Select(s => int.TryParse(s, out var n) ? n : 0).ToArray();
        var len = Math.Max(partsA.Length, partsB.Length);
        for (var i = 0; i < len; i++)
        {
            var x = i < partsA.Length ? partsA[i] : 0;
            var y = i < partsB.Length ? partsB[i] : 0;
            if (x != y) return x > y;
        }
        return false;
    }

    private void Raise([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
