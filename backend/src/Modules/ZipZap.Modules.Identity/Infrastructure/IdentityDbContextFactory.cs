using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace ZipZap.Modules.Identity.Infrastructure;

/// <summary>
/// Fabryka design-time dla `dotnet ef` (migracje). Connection string bierze ze
/// zmiennej środowiskowej lub używa domyślnego dev localhost.
/// </summary>
public sealed class IdentityDbContextFactory : IDesignTimeDbContextFactory<IdentityDbContext>
{
    public IdentityDbContext CreateDbContext(string[] args)
    {
        var conn = Environment.GetEnvironmentVariable("ConnectionStrings__Postgres")
                   ?? "Host=localhost;Port=5432;Database=zipzap;Username=zipzap;Password=zipzap";

        var options = new DbContextOptionsBuilder<IdentityDbContext>()
            .UseNpgsql(conn, npg => npg.MigrationsHistoryTable("__ef_migrations_history", IdentityDbContext.Schema))
            .Options;

        return new IdentityDbContext(options);
    }
}
