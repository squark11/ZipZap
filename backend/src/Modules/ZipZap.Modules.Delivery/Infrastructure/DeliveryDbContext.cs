using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using ZipZap.BuildingBlocks.Outbox;
using ZipZap.Modules.Delivery.Domain;

namespace ZipZap.Modules.Delivery.Infrastructure;

public sealed class DeliveryDbContext : DbContext, IOutboxDbContext
{
    public const string Schema = "delivery";

    public DeliveryDbContext(DbContextOptions<DeliveryDbContext> options) : base(options) { }

    public DbSet<Domain.Delivery> Deliveries => Set<Domain.Delivery>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.HasDefaultSchema(Schema);

        b.Entity<Domain.Delivery>(e =>
        {
            e.ToTable("deliveries");
            e.HasKey(d => d.Id);
            e.HasIndex(d => d.OrderId).IsUnique();
            e.HasIndex(d => new { d.StoreId, d.Status });
            e.HasIndex(d => d.Status); // pula dostępnych dostaw (filtr po samym statusie)
            e.HasIndex(d => d.DriverId);
            e.Property(d => d.Status).HasConversion<string>().HasMaxLength(24);
            // Ochrona przed równoczesnym przyjęciem dostawy przez dwóch kierowców.
#pragma warning disable CS0618
            e.UseXminAsConcurrencyToken();
#pragma warning restore CS0618
        });

        b.Entity<OutboxMessage>(e =>
        {
            e.ToTable("outbox_messages");
            e.HasKey(m => m.Id);
            e.Property(m => m.Type).IsRequired();
            e.Property(m => m.Payload).IsRequired();
            e.HasIndex(m => m.ProcessedAtUtc);
        });

        foreach (var key in b.Model.GetEntityTypes().SelectMany(t => t.GetDeclaredKeys()))
            foreach (var prop in key.Properties)
                if (prop.ClrType == typeof(Guid))
                    prop.ValueGenerated = ValueGenerated.Never;
    }
}
