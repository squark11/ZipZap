using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ZipZap.BuildingBlocks.Messaging;

namespace ZipZap.BuildingBlocks.Outbox;

/// <summary>
/// Generyczny procesor outboxa nad DbContextem modułu. Pobiera partię
/// nieprzetworzonych wiadomości, publikuje je na szynę i oznacza jako wysłane.
/// <list type="bullet">
/// <item>Błąd TRWAŁY (nieznany typ zdarzenia, nieczytelny ładunek) — ponowienie nic nie zmieni, więc wiadomość
/// od razu trafia do martwych (<see cref="OutboxMessage.DeadLetteredAtUtc"/>); zostaje w bazie do ręcznego ponowienia.</item>
/// <item>Każdy inny błąd (np. awaria bazy, brokera albo handlera) — ponawiany BEZ LIMITU prób, z narastającym
/// odstępem (10 s, 20 s, 40 s… maks. co 30 min). Wiadomość czekająca na termin nie blokuje kolejnych.</item>
/// <item>Wiele instancji API: partia jest pobierana atomowo z dzierżawą (<c>FOR UPDATE SKIP LOCKED</c>), więc dwie
/// instancje nie wysyłają tej samej wiadomości równolegle. Po awarii instancji dzierżawa wygasa i wiadomość wraca.</item>
/// </list>
/// Gwarancja „co najmniej raz" bez zmian: wiadomość jest oznaczana jako wysłana dopiero po udanej publikacji
/// (zapis po KAŻDEJ wiadomości, nie po całej partii). Duplikat jest możliwy tylko, gdy publikacja się udała, a zapis
/// znacznika nie (awaria w tej chwili) — handlery chroni wtedy deduplikacja w dyspozytorze zdarzeń.
/// </summary>
public sealed class OutboxProcessor<TContext> : IOutboxProcessor
    where TContext : DbContext, IOutboxDbContext
{
    public const int BatchSize = OutboxPolicy.BatchSize;

    public static readonly TimeSpan BaseRetryDelay = OutboxPolicy.BaseRetryDelay;
    public static readonly TimeSpan MaxRetryDelay = OutboxPolicy.MaxRetryDelay;

    /// <summary>Dzierżawa pobranej partii. Partia przerywa pracę po połowie tego czasu, reszta wraca do kolejki.</summary>
    public static readonly TimeSpan LeaseDuration = OutboxPolicy.LeaseDuration;

    private readonly TContext _db;
    private readonly IEventBus _bus;
    private readonly IIntegrationEventTypeRegistry _registry;
    private readonly ILogger<OutboxProcessor<TContext>> _logger;

    public OutboxProcessor(
        TContext db,
        IEventBus bus,
        IIntegrationEventTypeRegistry registry,
        ILogger<OutboxProcessor<TContext>> logger)
    {
        _db = db;
        _bus = bus;
        _registry = registry;
        _logger = logger;
    }

    public async Task ProcessPendingAsync(CancellationToken ct = default)
    {
        var claimedAt = DateTime.UtcNow;
        var messages = await ClaimBatchAsync(claimedAt, ct);
        if (messages.Count == 0) return;

        var pending = new Queue<OutboxMessage>(messages.OrderBy(m => m.OccurredAtUtc));
        try
        {
            while (pending.Count > 0 && DateTime.UtcNow - claimedAt < LeaseDuration / 2)
            {
                await ProcessOneAsync(pending.Peek(), ct);
                pending.Dequeue();
                await _db.SaveChangesAsync(ct);
            }
        }
        finally
        {
            // Nieobsłużona reszta partii (limit czasu dzierżawy, zamykanie aplikacji, błąd zapisu) — zwolnij od razu,
            // żeby nie czekała na wygaśnięcie dzierżawy. Jeśli i to się nie uda, dzierżawa wygaśnie sama.
            if (pending.Count > 0) await ReleaseAsync(pending);
        }
    }

    /// <summary>Atomowo pobiera partię wymagalnych wiadomości i zakłada na nie dzierżawę (pomija zablokowane).</summary>
    private Task<List<OutboxMessage>> ClaimBatchAsync(DateTime now, CancellationToken ct)
    {
        var t = new OutboxTable(_db);
        string id = t.Col(nameof(OutboxMessage.Id)), locked = t.Col(nameof(OutboxMessage.LockedUntilUtc)),
            next = t.Col(nameof(OutboxMessage.NextAttemptAtUtc));
        // {0} = koniec dzierżawy, {1} = teraz (parametry); reszta to nazwy z modelu EF.
        var sql = $$"""
            UPDATE {{t.Table}} SET {{locked}} = {0}
            WHERE {{id}} IN (
                SELECT {{id}} FROM {{t.Table}}
                WHERE {{t.Col(nameof(OutboxMessage.ProcessedAtUtc))}} IS NULL
                  AND {{t.Col(nameof(OutboxMessage.DeadLetteredAtUtc))}} IS NULL
                  AND ({{next}} IS NULL OR {{next}} <= {1})
                  AND ({{locked}} IS NULL OR {{locked}} <= {1})
                ORDER BY {{t.Col(nameof(OutboxMessage.OccurredAtUtc))}}
                LIMIT {{BatchSize}}
                FOR UPDATE SKIP LOCKED)
            RETURNING *
            """;
        return _db.OutboxMessages.FromSqlRaw(sql, now + LeaseDuration, now).ToListAsync(ct);
    }

    private async Task ProcessOneAsync(OutboxMessage message, CancellationToken ct)
    {
        // Do logów i do pola Error trafiają tylko metadane i kod kategorii — nigdy ładunek ani treść wyjątku.
        if (!TryRead(message, out var @event, out var permanentCategory))
        {
            message.Attempts++;
            message.Error = permanentCategory;
            message.NextAttemptAtUtc = null;
            message.LockedUntilUtc = null;
            message.DeadLetteredAtUtc = DateTime.UtcNow;
            _logger.LogError(
                "Wiadomość outboxa {MessageId} ({Type}) odłożona do martwych — błąd trwały {Category}, ponowienie nic nie zmieni.",
                message.Id, message.Type, permanentCategory);
            return;
        }

        try
        {
            await _bus.PublishAsync(@event, ct);

            message.ProcessedAtUtc = DateTime.UtcNow;
            message.NextAttemptAtUtc = null;
            message.LockedUntilUtc = null;
            message.Error = null;
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw; // zamykanie aplikacji — to nie jest nieudana próba; wiadomość wraca do kolejki w finally
        }
        catch (Exception ex)
        {
            message.Attempts++;
            message.Error = OutboxErrorCategory.Classify(ex);
            message.NextAttemptAtUtc = DateTime.UtcNow + RetryDelay(message.Attempts);
            message.LockedUntilUtc = null;
            _logger.LogWarning(
                "Nie udało się wysłać wiadomości outboxa {MessageId} ({Type}), próba {Attempts}, kategoria {Category} ({ExceptionType}); kolejna po {NextAttemptAtUtc:o}.",
                message.Id, message.Type, message.Attempts, message.Error, ex.GetType().Name, message.NextAttemptAtUtc);
        }
    }

    private async Task ReleaseAsync(IEnumerable<OutboxMessage> unprocessed)
    {
        try
        {
            foreach (var m in unprocessed) m.LockedUntilUtc = null;
            await _db.SaveChangesAsync(CancellationToken.None);
        }
        catch (Exception ex)
        {
            _logger.LogWarning("Nie udało się zwolnić dzierżawy wiadomości outboxa ({ExceptionType}) — wrócą po jej wygaśnięciu.",
                ex.GetType().Name);
        }
    }

    /// <summary>Rozpoznanie wiadomości; false = błąd trwały (nieznany typ albo ładunek, którego nie da się odczytać).</summary>
    private bool TryRead(OutboxMessage message, out IIntegrationEvent @event, out string category)
    {
        @event = null!;
        category = OutboxErrorCategory.UnreadablePayload;
        var type = _registry.Resolve(message.Type);
        if (type is null)
        {
            category = OutboxErrorCategory.UnknownType;
            return false;
        }
        try
        {
            if (JsonSerializer.Deserialize(message.Payload, type) is IIntegrationEvent e)
            {
                @event = e;
                return true;
            }
        }
        catch (Exception ex) when (ex is JsonException or NotSupportedException or ArgumentException)
        {
            // nieczytelny ładunek — kategoria ustawiona wyżej
        }
        return false;
    }

    /// <summary>Odstęp po <paramref name="attempts"/> nieudanych próbach: 10 s, 20 s, 40 s… maks. 30 min.</summary>
    public static TimeSpan RetryDelay(int attempts)
    {
        var seconds = BaseRetryDelay.TotalSeconds * Math.Pow(2, Math.Max(0, attempts - 1));
        return TimeSpan.FromSeconds(Math.Min(seconds, MaxRetryDelay.TotalSeconds));
    }
}
