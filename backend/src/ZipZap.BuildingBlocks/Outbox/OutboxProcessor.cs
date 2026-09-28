using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ZipZap.BuildingBlocks.Messaging;

namespace ZipZap.BuildingBlocks.Outbox;

/// <summary>
/// Generyczny procesor outboxa nad DbContextem modułu. Pobiera partię
/// nieprzetworzonych wiadomości, publikuje je na szynę i oznacza jako wysłane.
/// Nieudana wiadomość jest ponawiana z wykładniczym odstępem, a po
/// <see cref="MaxAttempts"/> próbach odkładana do martwych — dzięki temu
/// wiadomości „trujące" nie blokują kolejnych wiadomości modułu.
/// </summary>
public sealed class OutboxProcessor<TContext> : IOutboxProcessor
    where TContext : DbContext, IOutboxDbContext
{
    public const int BatchSize = 20;
    public const int MaxAttempts = 10;

    private static readonly TimeSpan BaseRetryDelay = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan MaxRetryDelay = TimeSpan.FromMinutes(30);

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
            try
            {
                var type = _registry.Resolve(message.Type)
                    ?? throw new InvalidOperationException($"Nieznany typ zdarzenia '{message.Type}'.");

                var @event = (IIntegrationEvent)JsonSerializer.Deserialize(message.Payload, type)!;
                await _bus.PublishAsync(@event, ct);

                message.ProcessedAtUtc = DateTime.UtcNow;
                message.NextAttemptAtUtc = null;
                message.Error = null;
            }
            catch (Exception ex)
            {
                message.Attempts++;
                message.Error = ex.Message;

                // Do logów trafiają tylko metadane — nigdy ładunek ani treść wyjątku (mogą zawierać dane osobowe).
                if (message.Attempts >= MaxAttempts)
                {
                    message.DeadLetteredAtUtc = DateTime.UtcNow;
                    message.NextAttemptAtUtc = null;
                    _logger.LogError(
                        "Wiadomość outboxa {MessageId} ({Type}) odłożona do martwych po {Attempts} próbach ({ExceptionType}).",
                        message.Id, message.Type, message.Attempts, ex.GetType().Name);
                }
                else
                {
                    message.NextAttemptAtUtc = DateTime.UtcNow + RetryDelay(message.Attempts);
                    _logger.LogWarning(
                        "Nie udało się wysłać wiadomości outboxa {MessageId} ({Type}), próba {Attempts}/{MaxAttempts} ({ExceptionType}); kolejna po {NextAttemptAtUtc:o}.",
                        message.Id, message.Type, message.Attempts, MaxAttempts, ex.GetType().Name, message.NextAttemptAtUtc);
                }
            }
        }

        await _db.SaveChangesAsync(ct);
    }

    /// <summary>Odstęp po <paramref name="attempts"/> nieudanych próbach: 10 s, 20 s, 40 s… maks. 30 min.</summary>
    private static TimeSpan RetryDelay(int attempts)
    {
        var seconds = BaseRetryDelay.TotalSeconds * Math.Pow(2, attempts - 1);
        return TimeSpan.FromSeconds(Math.Min(seconds, MaxRetryDelay.TotalSeconds));
    }
}
