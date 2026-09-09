using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ZipZap.BuildingBlocks.Auditing;
using ZipZap.BuildingBlocks.Persistence;
using ZipZap.Modules.Audit.Infrastructure;

namespace ZipZap.Modules.Audit;

public static class AuditModule
{
    public static IServiceCollection AddAuditModule(this IServiceCollection services, IConfiguration config)
    {
        var conn = config.GetConnectionString("Postgres")
            ?? throw new InvalidOperationException("Brak connection stringa 'Postgres'.");

        services.AddDbContext<AuditDbContext>(o =>
            o.UseNpgsql(conn, npg => npg.MigrationsHistoryTable("__ef_migrations_history", AuditDbContext.Schema)));

        services.AddScoped<IModuleDbMigrator, EfCoreModuleMigrator<AuditDbContext>>();
        services.AddScoped<IAuditLogger, EfAuditLogger>();

        return services;
    }
}
