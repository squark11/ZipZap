using Microsoft.EntityFrameworkCore;

namespace ZipZap.BuildingBlocks.Inbox;

/// <summary>
/// Inbox konsumenta: pozwala pominąć zdarzenie już przetworzone (deduplikacja
/// przy redostarczeniu / at-least-once). Kluczem jest Id zdarzenia integracyjnego.
/// </summary>
public interface IInboxStore
{
    Task<bool> HasProcessedAsync(Guid messageId, CancellationToken ct = default);
    Task MarkProcessedAsync(Guid messageId, string type, CancellationToken ct = default);
}

public sealed class EfInboxStore : IInboxStore
{
    private readonly MessagingDbContext _db;

    public EfInboxStore(MessagingDbContext db) => _db = db;

    public Task<bool> HasProcessedAsync(Guid messageId, CancellationToken ct = default)
        => _db.Inbox.AsNoTracking().AnyAsync(m => m.Id == messageId, ct);

    public async Task MarkProcessedAsync(Guid messageId, string type, CancellationToken ct = default)
    {
        _db.Inbox.Add(new InboxMessage { Id = messageId, Type = type, ProcessedAtUtc = DateTime.UtcNow });
        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            // Wyścig: inny wątek zdążył zapisać ten sam Id — traktuj jako już przetworzone.
            _db.ChangeTracker.Clear();
        }
    }
}
