using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace ZipZap.Modules.Notifications.Infrastructure;

public sealed class NotificationsDbContextFactory : IDesignTimeDbContextFactory<NotificationsDbContext>
{
    public NotificationsDbContext CreateDbContext(string[] args)
    {
        var conn = Environment.GetEnvironmentVariable("ConnectionStrings__Postgres")
                   ?? "Host=localhost;Port=5432;Database=zipzap;Username=zipzap;Password=zipzap";

        var options = new DbContextOptionsBuilder<NotificationsDbContext>()
            .UseNpgsql(conn, npg => npg.MigrationsHistoryTable("__ef_migrations_history", NotificationsDbContext.Schema))
            .Options;

        return new NotificationsDbContext(options);
    }
}
