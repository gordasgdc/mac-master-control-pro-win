using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace MacMasterControlPro.Core.Services.Access;

/// Ediția comunitară: fără core comercial și fără activare. Demo: sesiuni de 10 minute de utilizare activă, următoarea la 48 de ore
/// după epuizare. Starea e protejată cu DPAPI + HMAC; ceasul dat înapoi nu deblochează; starea coruptă = sesiune epuizată.
public sealed class CommunityAccess : IAccessProvider
{
    public const int SessionSeconds = 600, CooldownSeconds = 172_800, MaxTick = 5;

    private sealed class State
    {
        public int Consumed { get; set; }
        public double? ExhaustedAt { get; set; }
        public double MaxSeenNow { get; set; }
    }

    private readonly string _account;
    private readonly Func<DateTimeOffset> _now;
    private State _s = new();
    private DateTimeOffset? _lastTick;
    public event Action? Changed;

    public CommunityAccess(string account = "demo-state", Func<DateTimeOffset>? now = null)
    {
        _account = account; _now = now ?? (() => DateTimeOffset.UtcNow);
        Load(); Reconcile();
    }

    private double Trusted => Math.Max(_now().ToUnixTimeMilliseconds() / 1000.0, _s.MaxSeenNow);

    public AccessMode Mode
    {
        get
        {
            if (_s.ExhaustedAt is { } ex)
            {
                var next = ex + CooldownSeconds;
                if (Trusted >= next) return new AccessMode.DemoAvailable(SessionSeconds);   // sesiune nouă
                return new AccessMode.DemoLocked(DateTimeOffset.FromUnixTimeMilliseconds((long)(next * 1000)));
            }
            return new AccessMode.DemoAvailable(Math.Max(0, SessionSeconds - _s.Consumed));
        }
    }

    public IReadOnlyList<OfferSummary> Offers => Array.Empty<OfferSummary>();
    public bool IsOfficialEdition => false;
    public string ActivationRequestText() => CoreL.X($"Cerere de licență Master Control Studio Pro. Machine ID: {MachineID.Display}", $"Master Control Studio Pro licence request. Machine ID: {MachineID.Display}", $"Solicitud de licencia de Master Control Studio Pro. Machine ID: {MachineID.Display}");
    public Task<string?> SubmitLicenseAsync(string text) => Task.FromResult<string?>(CoreL.X("Ediția comunitară nu are activare. Licențele se aplică în ediția oficială distribuită de GDC.", "The community edition has no activation. Licences apply in the official edition distributed by GDC.", "La edición comunitaria no tiene activación. Las licencias se aplican en la edición oficial distribuida por GDC."));
    public Task RefreshAsync() => Task.CompletedTask;

    public void Tick(bool active)
    {
        var t = _now();
        try
        {
            if (_s.ExhaustedAt is { } ex && Trusted >= ex + CooldownSeconds) { _s = new State { MaxSeenNow = _s.MaxSeenNow }; Save(); Changed?.Invoke(); }
            if (Mode is not AccessMode.DemoAvailable || !active) return;
            var dt = _lastTick is { } l ? Math.Min(MaxTick, Math.Max(0, (int)Math.Round((t - l).TotalSeconds))) : 0;
            _s.Consumed += dt;
            if (_s.Consumed >= SessionSeconds) { _s.Consumed = SessionSeconds; _s.ExhaustedAt = Trusted; }
            Save(); Changed?.Invoke();
        }
        finally { _lastTick = t; Reconcile(); }
    }

    private void Reconcile() { var n = _now().ToUnixTimeMilliseconds() / 1000.0; if (n > _s.MaxSeenNow) _s.MaxSeenNow = n; }

    // ---- persistență: DPAPI (SecretStore) + HMAC ----
    private byte[] Key()
    {
        if (SecretStore.Get(_account + ".key") is { } k) return Convert.FromBase64String(k);
        var b = RandomNumberGenerator.GetBytes(32); SecretStore.Set(_account + ".key", Convert.ToBase64String(b)); return b;
    }
    private void Load()
    {
        var raw = SecretStore.Get(_account);
        if (raw is null) return;
        try
        {
            var d = Convert.FromBase64String(raw);
            if (d.Length <= 32) throw new InvalidDataException();
            var mac = d[..32]; var body = d[32..];
            if (!CryptographicOperations.FixedTimeEquals(mac, HMACSHA256.HashData(Key(), body))) throw new InvalidDataException();
            _s = JsonSerializer.Deserialize<State>(body) ?? new State();
        }
        catch
        {
            var n = _now().ToUnixTimeMilliseconds() / 1000.0;
            _s = new State { Consumed = SessionSeconds, ExhaustedAt = n, MaxSeenNow = n };
            DiagnosticLog.Log("warning", "access", "state.invalid"); Save();
        }
    }
    private void Save()
    {
        var body = JsonSerializer.SerializeToUtf8Bytes(_s);
        SecretStore.Set(_account, Convert.ToBase64String(HMACSHA256.HashData(Key(), body).Concat(body).ToArray()));
    }
}
