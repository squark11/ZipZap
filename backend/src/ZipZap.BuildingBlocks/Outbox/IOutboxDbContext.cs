using Microsoft.EntityFrameworkCore;

namespace ZipZap.BuildingBlocks.Outbox;

/// <summary>
/// Kontrakt DbContextu, który posiada tabelę outboxa. Implementowany przez
/// DbContext każdego modułu publikującego zdarzenia.
/// </summary>
public interface IOutboxDbContext
{
    DbSet<OutboxMessage> OutboxMessages { get; }
    Task<int> SaveChangesAsync(CancellationToken ct = default);
}
