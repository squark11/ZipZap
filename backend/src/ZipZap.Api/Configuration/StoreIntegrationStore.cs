using System.Text.Json;
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
/// Store pilotażowy w pliku JSON; wymienialny na DB bez zmian w API/panelu.
/// </summary>
public sealed class StoreIntegrationStore
{
    private readonly string _path;
    private readonly IDataProtector _dp;
    private readonly SemaphoreSlim _lock = new(1, 1);
    private static readonly JsonSerializerOptions _json = new() { WriteIndented = true };

    public StoreIntegrationStore(IHostEnvironment env, IDataProtectionProvider dpp)
    {
        var dir = Path.Combine(env.ContentRootPath, "App_Data");
        Directory.CreateDirectory(dir);
        _path = Path.Combine(dir, "store-integrations.json");
        _dp = dpp.CreateProtector("ZipZap.StorePaymentIntegration.v1");
    }

    private async Task<Dictionary<string, StorePaymentIntegration>> ReadAllAsync(CancellationToken ct)
    {
        if (!File.Exists(_path)) return new();
        try
        {
            await using var s = File.OpenRead(_path);
            return await JsonSerializer.DeserializeAsync<Dictionary<string, StorePaymentIntegration>>(s, _json, ct) ?? new();
        }
        catch { return new(); }
    }

    private static StoreIntegrationStatus ToStatus(StorePaymentIntegration i) =>
        new(i.Provider, i.MerchantId, i.PosId, i.Sandbox,
            !string.IsNullOrEmpty(i.ApiKeyEnc), !string.IsNullOrEmpty(i.CrcKeyEnc));

    public async Task<StoreIntegrationStatus> GetStatusAsync(Guid storeId, CancellationToken ct = default)
    {
        var all = await ReadAllAsync(ct);
        return all.TryGetValue(storeId.ToString(), out var i)
            ? ToStatus(i)
            : new StoreIntegrationStatus("", null, null, true, false, false);
    }

    public async Task<StoreIntegrationStatus> SaveAsync(Guid storeId, StoreIntegrationUpdate u, CancellationToken ct = default)
    {
        await _lock.WaitAsync(ct);
        try
        {
            var all = await ReadAllAsync(ct);
            all.TryGetValue(storeId.ToString(), out var existing);

            string? enc(string? plain, string? keepEnc) =>
                string.IsNullOrWhiteSpace(plain) ? keepEnc : _dp.Protect(plain.Trim());

            var updated = new StorePaymentIntegration
            {
                Provider = (u.Provider ?? "").Trim(),
                MerchantId = string.IsNullOrWhiteSpace(u.MerchantId) ? null : u.MerchantId!.Trim(),
                PosId = string.IsNullOrWhiteSpace(u.PosId) ? null : u.PosId!.Trim(),
                Sandbox = u.Sandbox,
                ApiKeyEnc = enc(u.ApiKey, existing?.ApiKeyEnc),
                CrcKeyEnc = enc(u.CrcKey, existing?.CrcKeyEnc),
            };
            all[storeId.ToString()] = updated;

            await using var s = File.Create(_path);
            await JsonSerializer.SerializeAsync(s, all, _json, ct);
            return ToStatus(updated);
        }
        finally { _lock.Release(); }
    }
}
