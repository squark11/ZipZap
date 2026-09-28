using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace ZipZap.BuildingBlocks.Inbox;

/// <summary>
/// Inbox konsumenta: pozwala pominąć zdarzenie już przetworzone (deduplikacja
/// przy redostarczeniu / at-least-once). Kluczem jest Id zdarzenia integracyjnego
/// (albo — w dyspozytorze — para zdarzenie + handler).
/// To NIE jest „dokładnie raz": jeśli handler wykonał skutek (np. wysłał powiadomienie), a zapis znacznika się
/// nie udał, błąd jest zgłaszany, zdarzenie wraca do ponowienia i handler może wykonać skutek drugi raz.
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
        catch (DbUpdateException ex) when (IsDuplicateKey(ex))
        {
            // Wyścig: inny wątek/instancja zdążyła zapisać ten sam klucz — to jest oczekiwany duplikat.
            _db.ChangeTracker.Clear();
        }
        catch
        {
            // Każdy inny błąd bazy to awaria zapisu znacznika — NIE udajemy duplikatu; wywołujący musi go zobaczyć.
            _db.ChangeTracker.Clear();
            throw;
        }
    }

    /// <summary>Tylko naruszenie klucza unikalnego (23505) jest „już zapisane"; brak połączenia, limit długości itp. — nie.</summary>
    public static bool IsDuplicateKey(DbUpdateException ex)
        => ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation };
}
