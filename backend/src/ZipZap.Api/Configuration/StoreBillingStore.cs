namespace ZipZap.Api.Configuration;

/// <summary>
/// Plan rozliczeniowy sklepu:
///  A = dostawę realizuje ZipZap → faktura = liczba dostaw × stała opłata (ZipZapDeliveryFee),
///  B = sklep ma własnego kuriera → faktura = suma prowizji z dostarczonych zamówień.
/// Domyślnie A (jak ustaliliśmy dla istniejących sklepów).
/// </summary>
public sealed record StoreBilling
{
    public string Plan { get; init; } = "A";
}

/// <summary>Plan rozliczeniowy — jeden dokument konfiguracji na sklep (<see cref="IConfigDocuments"/>).</summary>
public sealed class StoreBillingStore
{
    public const string KeyPrefix = "store-billing:";
    private readonly IConfigDocuments _docs;

    public StoreBillingStore(IConfigDocuments docs) => _docs = docs;

    public async Task<StoreBilling> GetAsync(Guid storeId, CancellationToken ct = default)
        => await _docs.GetAsync<StoreBilling>(KeyPrefix + storeId, ct) ?? new StoreBilling();

    public Task<StoreBilling> SaveAsync(Guid storeId, StoreBilling b, CancellationToken ct = default)
    {
        var clean = new StoreBilling { Plan = string.Equals(b.Plan, "B", StringComparison.OrdinalIgnoreCase) ? "B" : "A" };
        return _docs.UpdateAsync<StoreBilling>(KeyPrefix + storeId, _ => clean, ct);
    }
}
