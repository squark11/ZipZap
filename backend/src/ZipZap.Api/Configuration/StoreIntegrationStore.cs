using Microsoft.AspNetCore.DataProtection;

namespace ZipZap.Api.Configuration;

/// <summary>Zapis integracji płatności sklepu (sekrety SZYFROWANE at-rest).</summary>
internal sealed record StorePaymentIntegration
{
    public string Provider { get; init; } = "";
    public string? MerchantId { get; init; }
    public string? PosId { get; init; }
    public bool Sandbox { get; init; } = true;
    public string? ApiKeyEnc { get; init; }   // zaszyfrowane
    public string? CrcKeyEnc { get; init; }    // zaszyfrowane
}

/// <summary>Status zwracany do panelu — BEZ sekretów (tylko flagi „ustawione").</summary>
public sealed record StoreIntegrationStatus(
    string Provider, string? MerchantId, string? PosId, bool Sandbox, bool HasApiKey, bool HasCrcKey);

/// <summary>Dane z panelu. Puste pole sekretu = zachowaj istniejący.</summary>
public sealed record StoreIntegrationUpdate(
    string? Provider, string? MerchantId, string? PosId, bool Sandbox, string? ApiKey, string? CrcKey);

/// <summary>
/// Per-store integracja bramki płatniczej. Każdy sklep podpina SWOJE konto — tokeny
/// trzymamy zaszyfrowane (IDataProtector), nigdy nie zwracamy ich do klienta (write-only).
/// Jeden dokument konfiguracji na sklep (<see cref="IConfigDocuments"/>).
/// </summary>
public sealed class StoreIntegrationStore
{
    public const string KeyPrefix = "store-integrations:";
    private readonly IConfigDocuments _docs;
    private readonly IDataProtector _dp;

    public StoreIntegrationStore(IConfigDocuments docs, IDataProtectionProvider dpp)
    {
        _docs = docs;
        _dp = dpp.CreateProtector("ZipZap.StorePaymentIntegration.v1");
    }

    private static StoreIntegrationStatus ToStatus(StorePaymentIntegration i) =>
        new(i.Provider, i.MerchantId, i.PosId, i.Sandbox,
            !string.IsNullOrEmpty(i.ApiKeyEnc), !string.IsNullOrEmpty(i.CrcKeyEnc));

    public async Task<StoreIntegrationStatus> GetStatusAsync(Guid storeId, CancellationToken ct = default)
    {
        var i = await _docs.GetAsync<StorePaymentIntegration>(KeyPrefix + storeId, ct);
        return i is not null ? ToStatus(i) : new StoreIntegrationStatus("", null, null, true, false, false);
    }

    public async Task<StoreIntegrationStatus> SaveAsync(Guid storeId, StoreIntegrationUpdate u, CancellationToken ct = default)
    {
        string? enc(string? plain, string? keepEnc) =>
            string.IsNullOrWhiteSpace(plain) ? keepEnc : _dp.Protect(plain.Trim());

        var updated = await _docs.UpdateAsync<StorePaymentIntegration>(KeyPrefix + storeId, existing => new StorePaymentIntegration
        {
            Provider = (u.Provider ?? "").Trim(),
            MerchantId = string.IsNullOrWhiteSpace(u.MerchantId) ? null : u.MerchantId!.Trim(),
            PosId = string.IsNullOrWhiteSpace(u.PosId) ? null : u.PosId!.Trim(),
            Sandbox = u.Sandbox,
            ApiKeyEnc = enc(u.ApiKey, existing?.ApiKeyEnc),
            CrcKeyEnc = enc(u.CrcKey, existing?.CrcKeyEnc),
        }, ct);
        return ToStatus(updated);
    }
}
