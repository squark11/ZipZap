using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using ZipZap.BuildingBlocks.MultiTenancy;

namespace ZipZap.Modules.Catalog.Infrastructure;

/// <summary>Fabryka design-time dla `dotnet ef` (migracje). Bez filtra najemcy.</summary>
public sealed class CatalogDbContextFactory : IDesignTimeDbContextFactory<CatalogDbContext>
{
    public CatalogDbContext CreateDbContext(string[] args)
    {
        var conn = Environment.GetEnvironmentVariable("ConnectionStrings__Postgres")
                   ?? "Host=localhost;Port=5432;Database=zipzap;Username=zipzap;Password=zipzap";

        var options = new DbContextOptionsBuilder<CatalogDbContext>()
            .UseNpgsql(conn, npg => npg.MigrationsHistoryTable("__ef_migrations_history", CatalogDbContext.Schema))
            .Options;

        return new CatalogDbContext(options, new CurrentTenant());
    }
}
