using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using ZipZap.BuildingBlocks.Outbox;
using ZipZap.Modules.Payments.Domain;

namespace ZipZap.Modules.Payments.Infrastructure;

public sealed class PaymentsDbContext : DbContext, IOutboxDbContext
{
    public const string Schema = "payments";

    public PaymentsDbContext(DbContextOptions<PaymentsDbContext> options) : base(options) { }

    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<CommissionLedgerEntry> CommissionLedger => Set<CommissionLedgerEntry>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.HasDefaultSchema(Schema);

        b.Entity<Payment>(e =>
        {
            e.ToTable("payments");
            e.HasKey(p => p.Id);
            e.HasIndex(p => p.OrderId).IsUnique();
            e.Property(p => p.Amount).HasColumnType("numeric(12,2)");
            e.Property(p => p.CommissionAmount).HasColumnType("numeric(12,2)");
            e.Property(p => p.Status).HasConversion<string>().HasMaxLength(20);
            e.Property(p => p.ProviderRef).HasMaxLength(64);
        });

        b.Entity<CommissionLedgerEntry>(e =>
        {
            e.ToTable("commission_ledger");
            e.HasKey(l => l.Id);
            e.HasIndex(l => l.StoreId);
            e.Property(l => l.Amount).HasColumnType("numeric(12,2)");
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
