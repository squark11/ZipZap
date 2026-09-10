using ZipZap.Modules.Payments.Application;

namespace ZipZap.Api.Configuration;

/// <summary>
/// Adapter portu <see cref="IStorePaymentGateway"/> nad magazynem integracji sklepów.
/// Zwraca klucz dostawcy skonfigurowany przez sklep (bez sekretów) — routing płatności
/// per-store. Sekrety pozostają w magazynie (odszyfrowywane dopiero przez realny adapter).
/// </summary>
public sealed class StorePaymentGatewayAdapter : IStorePaymentGateway
{
    private readonly StoreIntegrationStore _store;
    public StorePaymentGatewayAdapter(StoreIntegrationStore store) => _store = store;

    public async Task<string?> GetProviderKeyAsync(Guid storeId, CancellationToken ct = default)
    {
        var status = await _store.GetStatusAsync(storeId, ct);
        return string.IsNullOrWhiteSpace(status.Provider) ? null : status.Provider;
    }
}
