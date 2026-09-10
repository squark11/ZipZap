namespace ZipZap.Modules.Payments.Application;

/// <summary>
/// Port: klucz dostawcy płatności skonfigurowanego przez dany sklep (per-store bramka).
/// Host dostarcza adapter nad magazynem integracji sklepów; brak konfiguracji → null,
/// czyli używamy dostawcy domyślnego platformy.
/// </summary>
public interface IStorePaymentGateway
{
    Task<string?> GetProviderKeyAsync(Guid storeId, CancellationToken ct = default);
}

/// <summary>Domyślnie: brak per-store konfiguracji (dostawca domyślny).</summary>
public sealed class NullStorePaymentGateway : IStorePaymentGateway
{
    public Task<string?> GetProviderKeyAsync(Guid storeId, CancellationToken ct = default)
        => Task.FromResult<string?>(null);
}
