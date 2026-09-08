namespace ZipZap.Modules.Payments.Application;

public enum PaymentOutcome { Unknown, Authorized, Failed }

public sealed record PaymentSessionRequest(Guid PaymentId, Guid OrderId, decimal Amount, string Currency, string Description);

public sealed record PaymentSession(string SessionId, string RedirectUrl);

public sealed record WebhookResult(bool Verified, Guid PaymentId, PaymentOutcome Outcome, string? ProviderRef);

/// <summary>
/// Port bramki płatności. Warstwa biznesowa zależy tylko od tej abstrakcji —
/// nigdy od SDK dostawcy. Realne bramki (Przelewy24, Fiserv) = kolejne adaptery.
/// </summary>
public interface IPaymentProvider
{
    string Key { get; }

    /// <summary>Tworzy sesję płatności i zwraca URL przekierowania dla klienta.</summary>
    Task<PaymentSession> CreateSessionAsync(PaymentSessionRequest request, CancellationToken ct = default);

    /// <summary>Weryfikuje podpis webhooka i parsuje wynik (autorytatywne źródło statusu).</summary>
    WebhookResult VerifyWebhook(string rawBody, string? signature);
}

/// <summary>Rejestr dostawców po kluczu (wybór per webhook / domyślny do sesji).</summary>
public sealed class PaymentProviderRegistry
{
    private readonly Dictionary<string, IPaymentProvider> _providers;
    private readonly string _defaultKey;

    public PaymentProviderRegistry(IEnumerable<IPaymentProvider> providers, string defaultKey)
    {
        _providers = providers.ToDictionary(p => p.Key, StringComparer.OrdinalIgnoreCase);
        _defaultKey = defaultKey;
    }

    public IPaymentProvider? Get(string key) => _providers.GetValueOrDefault(key);

    public IPaymentProvider Default =>
        _providers.GetValueOrDefault(_defaultKey)
        ?? _providers.Values.FirstOrDefault()
        ?? throw new InvalidOperationException("Brak zarejestrowanego dostawcy płatności.");
}
