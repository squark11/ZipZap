using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using ZipZap.Modules.Notifications.Domain;

namespace ZipZap.Modules.Notifications.Infrastructure;

public sealed class NotificationsDbContext : DbContext
{
    public const string Schema = "notifications";

    public NotificationsDbContext(DbContextOptions<NotificationsDbContext> options) : base(options) { }

    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<DeviceToken> DeviceTokens => Set<DeviceToken>();
    public DbSet<OrderRecipient> OrderRecipients => Set<OrderRecipient>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.HasDefaultSchema(Schema);

        b.Entity<Notification>(e =>
        {
            e.ToTable("notifications");
            e.HasKey(n => n.Id);
            e.Property(n => n.Channel).IsRequired().HasMaxLength(32);
            e.Property(n => n.Template).IsRequired().HasMaxLength(64);
            e.Property(n => n.Status).IsRequired().HasMaxLength(20);
            e.HasIndex(n => n.CreatedAtUtc);
            e.HasIndex(n => new { n.RecipientUserId, n.CreatedAtUtc });
        });

        b.Entity<DeviceToken>(e =>
        {
            e.ToTable("device_tokens");
            e.HasKey(t => t.Id);
            e.Property(t => t.Token).IsRequired().HasMaxLength(512);
            e.Property(t => t.Platform).IsRequired().HasMaxLength(16);
            e.HasIndex(t => t.UserId);
            e.HasIndex(t => t.Token).IsUnique();
        });

        b.Entity<OrderRecipient>(e =>
        {
            e.ToTable("order_recipients");
            e.HasKey(r => r.OrderId);
            e.Property(r => r.OrderId).ValueGeneratedNever();
        });

        foreach (var key in b.Model.GetEntityTypes().SelectMany(t => t.GetDeclaredKeys()))
            foreach (var prop in key.Properties)
                if (prop.ClrType == typeof(Guid))
                    prop.ValueGenerated = ValueGenerated.Never;
    }
}
