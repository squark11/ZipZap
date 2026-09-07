using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ZipZap.BuildingBlocks.Messaging;

namespace ZipZap.BuildingBlocks.Outbox;

/// <summary>
/// Generyczny procesor outboxa nad DbContextem modułu. Pobiera partię
/// nieprzetworzonych wiadomości, publikuje je na szynę i oznacza jako wysłane.
/// </summary>
public sealed class OutboxProcessor<TContext> : IOutboxProcessor
    where TContext : DbContext, IOutboxDbContext
{
    private const int BatchSize = 20;

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
        var messages = await _db.OutboxMessages
            .Where(m => m.ProcessedAtUtc == null)
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
                message.Error = null;
            }
            catch (Exception ex)
            {
                message.Attempts++;
                message.Error = ex.Message;
                _logger.LogError(ex, "Nie udało się wysłać wiadomości outboxa {MessageId} ({Type})",
                    message.Id, message.Type);
            }
        }

        await _db.SaveChangesAsync(ct);
    }
}
