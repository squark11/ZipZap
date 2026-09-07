using Microsoft.EntityFrameworkCore;

namespace ZipZap.BuildingBlocks.Persistence;

/// <summary>
/// Uruchamia migracje DbContextu jednego modułu. Host wywołuje wszystkie
/// zarejestrowane migratory przy starcie (dev/CI convenience).
/// </summary>
public interface IModuleDbMigrator
{
    string ModuleName { get; }
    Task MigrateAsync(CancellationToken ct = default);
}

public sealed class EfCoreModuleMigrator<TContext> : IModuleDbMigrator
    where TContext : DbContext
{
    private readonly TContext _db;

    public EfCoreModuleMigrator(TContext db) => _db = db;

    public string ModuleName => typeof(TContext).Name;

    public Task MigrateAsync(CancellationToken ct = default) => _db.Database.MigrateAsync(ct);
}
