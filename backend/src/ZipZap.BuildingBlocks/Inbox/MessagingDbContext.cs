using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace ZipZap.BuildingBlocks.Inbox;

/// <summary>Kontekst infrastruktury komunikatów — schemat `messaging` (inbox konsumenta).</summary>
public sealed class MessagingDbContext : DbContext
{
    public const string Schema = "messaging";

    public MessagingDbContext(DbContextOptions<MessagingDbContext> options) : base(options) { }

    public DbSet<InboxMessage> Inbox => Set<InboxMessage>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.HasDefaultSchema(Schema);

        b.Entity<InboxMessage>(e =>
        {
            e.ToTable("inbox_messages");
            e.HasKey(m => m.Id);
            e.Property(m => m.Type).IsRequired().HasMaxLength(300);
            e.Property(m => m.ProcessedAtUtc);
        });

        foreach (var key in b.Model.GetEntityTypes().SelectMany(t => t.GetDeclaredKeys()))
            foreach (var prop in key.Properties)
                if (prop.ClrType == typeof(Guid))
                    prop.ValueGenerated = ValueGenerated.Never;
    }
}
