namespace MacMasterControlPro.Core.Services.Access;

/// Starea accesului la funcții: singura sursă pentru ce poate face aplicația acum.
public abstract record AccessMode
{
    /// Licență sau lease valid.
    public sealed record Full(string Source) : AccessMode;
    /// Sesiune de probă în curs: funcțiile merg cât timp există secunde de utilizare activă.
    public sealed record DemoAvailable(int RemainingSeconds) : AccessMode;
    /// Sesiunea s-a consumat; următoarea e disponibilă la `Next`. Aplicația rămâne deschisă.
    public sealed record DemoLocked(DateTimeOffset Next) : AccessMode;

    public bool AllowsFeatures => this is not DemoLocked;
    public bool IsFull => this is Full;
}

/// O ofertă de donație afișată utilizatorului; valorile vin din lista semnată (ediția oficială). Ediția comunitară nu afișează sume.
public sealed record OfferSummary(string Id, string Title, string AmountText);

/// Granița dintre aplicația publică (MIT) și componentele comerciale GDC. Ediția oficială furnizează o implementare legată la
/// core-ul privat în build-ul oficial; fără ea, aplicația rulează cu `CommunityAccess`.
public interface IAccessProvider
{
    AccessMode Mode { get; }
    IReadOnlyList<OfferSummary> Offers { get; }
    string EditionLabel { get; }
    string ActivationRequestText();
    /// Aplică o licență lipită de utilizator. Întoarce mesajul de eroare sau null la succes.
    Task<string?> SubmitLicenseAsync(string text);
    /// Chemat la ~1 Hz; `active` = utilizatorul folosește efectiv o funcție.
    void Tick(bool active);
    Task RefreshAsync();
    event Action? Changed;
}

public static class AccessFactory
{
    public static Func<IAccessProvider> Make { get; set; } = () => new CommunityAccess();
}
