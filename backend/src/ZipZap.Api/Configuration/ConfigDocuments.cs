using System.Text.Json;
using Microsoft.EntityFrameworkCore;

namespace ZipZap.Api.Configuration;

/// <summary>Trwałe dokumenty konfiguracji (JSON) — port, za którym stoją magazyny edytowane w panelu.</summary>
public interface IConfigDocuments
{
    /// <summary>Dokument albo null, gdy nigdy nie zapisany.</summary>
    Task<T?> GetAsync<T>(string key, CancellationToken ct = default) where T : class;

    /// <summary>Odczyt‑zmiana‑zapis jednego dokumentu jako całość (bez zgubionych aktualizacji przy równoległych zapisach).</summary>
    Task<T> UpdateAsync<T>(string key, Func<T?, T> update, CancellationToken ct = default) where T : class;
}

/// <summary>
/// Dokumenty konfiguracji w Postgresie (<see cref="PlatformConfigDbContext"/>) — przeżywają wdrożenia i są wspólne
/// dla wszystkich instancji API. Zapis: jedna transakcja pod blokadą doradczą klucza. Odczyt jest atomowy, a błąd bazy
/// albo nieczytelny dokument kończy się wyjątkiem — nigdy cichym „pustym" ustawieniem (np. wyłączoną captchą).
/// </summary>
public sealed class PostgresConfigDocuments : IConfigDocuments
{
    private static readonly JsonSerializerOptions Json = new();
    private readonly IServiceScopeFactory _scopes;

    public PostgresConfigDocuments(IServiceScopeFactory scopes) => _scopes = scopes;

    public async Task<T?> GetAsync<T>(string key, CancellationToken ct = default) where T : class
    {
        await using var scope = _scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformConfigDbContext>();
        var json = await db.Documents.AsNoTracking().Where(d => d.Key == key).Select(d => d.Json).FirstOrDefaultAsync(ct);
        return json is null ? null : JsonSerializer.Deserialize<T>(json, Json);
    }

    public async Task<T> UpdateAsync<T>(string key, Func<T?, T> update, CancellationToken ct = default) where T : class
    {
        await using var scope = _scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformConfigDbContext>();
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        // Blokada działa także dla dokumentu, którego jeszcze nie ma (dwa pierwsze zapisy nie ścigają się o INSERT).
        await db.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT pg_advisory_xact_lock(hashtextextended({"config:" + key}, 0))", ct);

        var doc = await db.Documents.FirstOrDefaultAsync(d => d.Key == key, ct);
        var next = update(doc is null ? null : JsonSerializer.Deserialize<T>(doc.Json, Json));
        var json = JsonSerializer.Serialize(next, Json);
        if (doc is null) db.Documents.Add(new ConfigDocument(key, json, DateTime.UtcNow));
        else doc.Replace(json, DateTime.UtcNow);

        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return next;
    }
}
