using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ZipZap.BuildingBlocks.Outbox;

/// <summary>
/// Wgląd administratora w outbox JEDNEGO modułu: tylko bezpieczne metadane (bez ładunku i bez treści błędów)
/// oraz ręczne ponowienie pojedynczej odłożonej wiadomości.
/// </summary>
public interface IOutboxAdministration
{
    /// <summary>Nazwa modułu (schemat bazy, np. „ordering").</summary>
    string Module { get; }

    Task<OutboxModuleOverview> GetOverviewAsync(int take, CancellationToken ct = default);

    /// <summary>
    /// Przywraca do kolejki odłożoną wiadomość, jeśli nadal jest odłożona i ma oczekiwaną liczbę prób (ochrona przed
    /// podwójnym kliknięciem i równoległym ponowieniem — warunkowy UPDATE, wygrywa dokładnie jedno żądanie).
    /// </summary>
    Task<OutboxRequeueResult> RequeueDeadLetteredAsync(Guid messageId, int expectedAttempts, Guid requestedBy,
        CancellationToken ct = default);
}

public enum OutboxRequeueResult { Requeued, NotFound, Conflict }

/// <summary>Metadane wiadomości do panelu — celowo BEZ ładunku i bez treści błędu (tylko kod kategorii).</summary>
public sealed record OutboxMessageInfo(
    string Module,
    Guid Id,
    string Type,
    DateTime OccurredAtUtc,
    DateTime? DeadLetteredAtUtc,
    DateTime? NextAttemptAtUtc,
    int Attempts,
    string ErrorCategory,
    string ErrorDescription,
    DateTime? LastManualRetryAtUtc,
    Guid? LastManualRetryByUserId);

public sealed record OutboxModuleOverview(
    string Module,
    int DeadLettered,
    int Retrying,
    int LongRetrying,
    IReadOnlyList<OutboxMessageInfo> DeadLetteredItems,
    IReadOnlyList<OutboxMessageInfo> LongRetryingItems);

public sealed class OutboxAdministration<TContext> : IOutboxAdministration
    where TContext : DbContext, IOutboxDbContext
{
    public const int LongRetryAttempts = OutboxPolicy.LongRetryAttempts;

    private readonly TContext _db;

    public OutboxAdministration(TContext db)
    {
        _db = db;
        Module = new OutboxTable(db).Module;
    }

    public string Module { get; }

    public async Task<OutboxModuleOverview> GetOverviewAsync(int take, CancellationToken ct = default)
    {
        var q = _db.OutboxMessages.AsNoTracking().Where(m => m.ProcessedAtUtc == null);
        var dead = q.Where(m => m.DeadLetteredAtUtc != null);
        var retrying = q.Where(m => m.DeadLetteredAtUtc == null && m.Attempts > 0);
        var longRetrying = retrying.Where(m => m.Attempts >= LongRetryAttempts);

        var deadItems = await Project(dead.OrderByDescending(m => m.DeadLetteredAtUtc).Take(take)).ToListAsync(ct);
        var longItems = await Project(longRetrying.OrderByDescending(m => m.Attempts).ThenBy(m => m.OccurredAtUtc).Take(take))
            .ToListAsync(ct);

        return new OutboxModuleOverview(Module,
            await dead.CountAsync(ct), await retrying.CountAsync(ct), await longRetrying.CountAsync(ct),
            deadItems.Select(ToInfo).ToList(), longItems.Select(ToInfo).ToList());
    }

    public async Task<OutboxRequeueResult> RequeueDeadLetteredAsync(Guid messageId, int expectedAttempts, Guid requestedBy,
        CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        // Warunkowy UPDATE jest atomowy: dwa równoległe żądania — pierwsze zmienia wiersz, drugie (po odczekaniu
        // na blokadę wiersza) widzi już DeadLetteredAtUtc = null i nie zmienia nic. Autor i czas zapisują się razem
        // z ponowieniem (nie osobnym, zawodnym zapisem).
        var changed = await _db.OutboxMessages
            .Where(m => m.Id == messageId && m.ProcessedAtUtc == null && m.DeadLetteredAtUtc != null
                        && m.Attempts == expectedAttempts)
            .ExecuteUpdateAsync(s => s
                .SetProperty(m => m.DeadLetteredAtUtc, (DateTime?)null)
                .SetProperty(m => m.NextAttemptAtUtc, (DateTime?)null)
                .SetProperty(m => m.LockedUntilUtc, (DateTime?)null)
                .SetProperty(m => m.LastManualRetryAtUtc, now)
                .SetProperty(m => m.LastManualRetryByUserId, requestedBy), ct);

        if (changed == 1) return OutboxRequeueResult.Requeued;
        return await _db.OutboxMessages.AnyAsync(m => m.Id == messageId, ct)
            ? OutboxRequeueResult.Conflict
            : OutboxRequeueResult.NotFound;
    }

    // Projekcja w SQL — ładunek (Payload) nigdy nie jest pobierany z bazy.
    private static IQueryable<Row> Project(IQueryable<OutboxMessage> q) => q.Select(m => new Row(
        m.Id, m.Type, m.OccurredAtUtc, m.DeadLetteredAtUtc, m.NextAttemptAtUtc, m.Attempts, m.Error,
        m.LastManualRetryAtUtc, m.LastManualRetryByUserId));

    private OutboxMessageInfo ToInfo(Row r)
    {
        var category = OutboxErrorCategory.Normalize(r.Error);
        return new OutboxMessageInfo(Module, r.Id, SafeType(r.Type), r.OccurredAtUtc, r.DeadLetteredAtUtc,
            r.NextAttemptAtUtc, r.Attempts, category, OutboxErrorCategory.Describe(category),
            r.LastManualRetryAtUtc, r.LastManualRetryByUserId);
    }

    /// <summary>Nazwa typu to nazwa klasy .NET z rejestru; przycinamy na wszelki wypadek (nieznane typy).</summary>
    private static string SafeType(string type) => type.Length <= 200 ? type : type[..200];

    private sealed record Row(Guid Id, string Type, DateTime OccurredAtUtc, DateTime? DeadLetteredAtUtc,
        DateTime? NextAttemptAtUtc, int Attempts, string? Error, DateTime? LastManualRetryAtUtc,
        Guid? LastManualRetryByUserId);
}

public static class OutboxServiceCollectionExtensions
{
    /// <summary>Outbox modułu: procesor (wysyłka) + wgląd administratora (panel odłożonych zdarzeń).</summary>
    public static IServiceCollection AddModuleOutbox<TContext>(this IServiceCollection services)
        where TContext : DbContext, IOutboxDbContext
    {
        services.AddScoped<IOutboxProcessor, OutboxProcessor<TContext>>();
        services.AddScoped<IOutboxAdministration, OutboxAdministration<TContext>>();
        return services;
    }
}
