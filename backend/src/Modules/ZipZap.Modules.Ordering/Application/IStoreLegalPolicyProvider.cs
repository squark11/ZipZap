namespace ZipZap.Modules.Ordering.Application;

/// <summary>
/// Polityka prawna sklepu wymagana do złożenia zamówienia: adresy dokumentów
/// (regulamin, polityka prywatności, RODO) + czy klient musi je zaakceptować.
/// </summary>
public sealed record StoreLegalPolicy(bool RequiresAcceptance, string? TermsUrl, string? PrivacyUrl, string? GdprUrl);

/// <summary>
/// Port: konfiguracja dokumentów prawnych sklepu. Host dostarcza adapter nad magazynem
/// dokumentów; brak konfiguracji → null (sklep bez wymogu akceptacji).
/// </summary>
public interface IStoreLegalPolicyProvider
{
    Task<StoreLegalPolicy?> GetAsync(Guid storeId, CancellationToken ct = default);
}

/// <summary>Domyślnie: brak polityki (sklep nie wymaga akceptacji dokumentów).</summary>
public sealed class NullStoreLegalPolicyProvider : IStoreLegalPolicyProvider
{
    public Task<StoreLegalPolicy?> GetAsync(Guid storeId, CancellationToken ct = default)
        => Task.FromResult<StoreLegalPolicy?>(null);
}
