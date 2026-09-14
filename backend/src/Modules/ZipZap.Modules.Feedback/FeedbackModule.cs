using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ZipZap.BuildingBlocks.Persistence;
using ZipZap.Modules.Feedback.Application;
using ZipZap.Modules.Feedback.Infrastructure;

namespace ZipZap.Modules.Feedback;

public static class FeedbackModule
{
    public static IServiceCollection AddFeedbackModule(this IServiceCollection services, IConfiguration config)
    {
        var conn = config.GetConnectionString("Postgres")
            ?? throw new InvalidOperationException("Brak connection stringa 'Postgres'.");

        services.AddDbContext<FeedbackDbContext>(o =>
            o.UseNpgsql(conn, npg => npg.MigrationsHistoryTable("__ef_migrations_history", FeedbackDbContext.Schema)));

        services.AddScoped<IModuleDbMigrator, EfCoreModuleMigrator<FeedbackDbContext>>();
        services.AddScoped<FeedbackService>();

        return services;
    }
}
