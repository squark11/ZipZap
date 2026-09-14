using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using ZipZap.Modules.Feedback.Domain;

namespace ZipZap.Modules.Feedback.Infrastructure;

/// <summary>DbContext modułu Feedback — własny schemat `feedback`.</summary>
public sealed class FeedbackDbContext : DbContext
{
    public const string Schema = "feedback";

    public FeedbackDbContext(DbContextOptions<FeedbackDbContext> options) : base(options) { }

    public DbSet<FeedbackItem> Items => Set<FeedbackItem>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.HasDefaultSchema(Schema);

        b.Entity<FeedbackItem>(e =>
        {
            e.ToTable("feedback_items");
            e.HasKey(x => x.Id);
            e.Property(x => x.Type).HasConversion<string>().HasMaxLength(16);
            e.Property(x => x.Status).HasConversion<string>().HasMaxLength(16);
            e.Property(x => x.Message).IsRequired().HasMaxLength(4000);
            e.Property(x => x.ContactEmail).HasMaxLength(256);
            e.Property(x => x.Screen).HasMaxLength(120);
            e.Property(x => x.AppVersion).HasMaxLength(40);
            e.Property(x => x.Platform).HasMaxLength(24);
            e.HasIndex(x => x.CreatedAtUtc);
            e.HasIndex(x => x.Status);
            e.HasIndex(x => x.StoreId);
        });

        foreach (var key in b.Model.GetEntityTypes().SelectMany(t => t.GetDeclaredKeys()))
            foreach (var prop in key.Properties)
                if (prop.ClrType == typeof(Guid))
                    prop.ValueGenerated = ValueGenerated.Never;
    }
}
