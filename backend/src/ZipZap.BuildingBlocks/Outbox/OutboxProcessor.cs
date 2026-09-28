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
/// </list>
/// Gwarancja „co najmniej raz" bez zmian: wiadomość jest oznaczana jako wysłana dopiero po udanej publikacji.
/// </summary>
public sealed class OutboxProcessor<TContext> : IOutboxProcessor
    where TContext : DbContext, IOutboxDbContext
{
    public const int BatchSize = 20;

    public static readonly TimeSpan BaseRetryDelay = TimeSpan.FromSeconds(10);
    public static readonly TimeSpan MaxRetryDelay = TimeSpan.FromMinutes(30);

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
        var now = DateTime.UtcNow;
        var messages = await _db.OutboxMessages
            .Where(m => m.ProcessedAtUtc == null
                        && m.DeadLetteredAtUtc == null
                        && (m.NextAttemptAtUtc == null || m.NextAttemptAtUtc <= now))
            .OrderBy(m => m.OccurredAtUtc)
            .Take(BatchSize)
            .ToListAsync(ct);

        if (messages.Count == 0) return;

        foreach (var message in messages)
        {
            // Do logów trafiają tylko metadane — nigdy ładunek ani treść wyjątku (mogą zawierać dane osobowe).
            if (!TryRead(message, out var @event, out var permanentError))
            {
                message.Attempts++;
                message.Error = permanentError;
                message.NextAttemptAtUtc = null;
                message.DeadLetteredAtUtc = DateTime.UtcNow;
                _logger.LogError(
                    "Wiadomość outboxa {MessageId} ({Type}) odłożona do martwych — błąd trwały, ponowienie nic nie zmieni.",
                    message.Id, message.Type);
                continue;
            }

            try
            {
                await _bus.PublishAsync(@event, ct);

                message.ProcessedAtUtc = DateTime.UtcNow;
                message.NextAttemptAtUtc = null;
                message.Error = null;
            }
            catch (Exception ex)
            {
                message.Attempts++;
                message.Error = ex.Message;
                message.NextAttemptAtUtc = DateTime.UtcNow + RetryDelay(message.Attempts);
                _logger.LogWarning(
                    "Nie udało się wysłać wiadomości outboxa {MessageId} ({Type}), próba {Attempts} ({ExceptionType}); kolejna po {NextAttemptAtUtc:o}.",
                    message.Id, message.Type, message.Attempts, ex.GetType().Name, message.NextAttemptAtUtc);
            }
        }

        await _db.SaveChangesAsync(ct);
    }

    /// <summary>Rozpoznanie wiadomości; false = błąd trwały (nieznany typ albo ładunek, którego nie da się odczytać).</summary>
    private bool TryRead(OutboxMessage message, out IIntegrationEvent @event, out string error)
    {
        @event = null!;
        error = string.Empty;
        var type = _registry.Resolve(message.Type);
        if (type is null)
        {
            error = $"Nieznany typ zdarzenia '{message.Type}'.";
            return false;
        }
        try
        {
            if (JsonSerializer.Deserialize(message.Payload, type) is IIntegrationEvent e)
            {
                @event = e;
                return true;
            }
            error = "Pusty ładunek zdarzenia.";
        }
        catch (Exception ex) when (ex is JsonException or NotSupportedException or ArgumentException)
        {
            error = $"Nieczytelny ładunek zdarzenia ({ex.GetType().Name}).";
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
