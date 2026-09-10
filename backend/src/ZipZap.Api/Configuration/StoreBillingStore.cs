using System.Text.Json;

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

public sealed class StoreBillingStore
{
    private readonly string _path;
    private readonly SemaphoreSlim _lock = new(1, 1);
    private static readonly JsonSerializerOptions _json = new() { WriteIndented = true };

    public StoreBillingStore(IHostEnvironment env)
    {
        var dir = Path.Combine(env.ContentRootPath, "App_Data");
        Directory.CreateDirectory(dir);
        _path = Path.Combine(dir, "store-billing.json");
    }

    private async Task<Dictionary<string, StoreBilling>> ReadAllAsync(CancellationToken ct)
    {
        if (!File.Exists(_path)) return new();
        try
        {
            await using var s = File.OpenRead(_path);
            return await JsonSerializer.DeserializeAsync<Dictionary<string, StoreBilling>>(s, _json, ct) ?? new();
        }
        catch { return new(); }
    }

    public async Task<StoreBilling> GetAsync(Guid storeId, CancellationToken ct = default)
    {
        var all = await ReadAllAsync(ct);
        return all.TryGetValue(storeId.ToString(), out var b) ? b : new StoreBilling();
    }

    public async Task<StoreBilling> SaveAsync(Guid storeId, StoreBilling b, CancellationToken ct = default)
    {
        var clean = new StoreBilling { Plan = string.Equals(b.Plan, "B", StringComparison.OrdinalIgnoreCase) ? "B" : "A" };
        await _lock.WaitAsync(ct);
        try
        {
            var all = await ReadAllAsync(ct);
            all[storeId.ToString()] = clean;
            await using var s = File.Create(_path);
            await JsonSerializer.SerializeAsync(s, all, _json, ct);
        }
        finally { _lock.Release(); }
        return clean;
    }
}
