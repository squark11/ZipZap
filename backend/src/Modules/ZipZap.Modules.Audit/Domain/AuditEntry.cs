using ZipZap.BuildingBlocks.Domain;

namespace ZipZap.Modules.Audit.Domain;

/// <summary>Wpis audytu: kto (aktor), co (akcja), na czym (encja), kiedy.</summary>
public sealed class AuditEntry : Entity
{
    public Guid? ActorUserId { get; private set; }
    public string Action { get; private set; } = default!;
    public string EntityType { get; private set; } = default!;
    public string? EntityId { get; private set; }
    public Guid? StoreId { get; private set; }
    public string? Details { get; private set; }
    public DateTime CreatedAtUtc { get; private set; } = DateTime.UtcNow;

    private AuditEntry() { } // EF

    public AuditEntry(Guid? actorUserId, string action, string entityType,
        string? entityId, Guid? storeId, string? details)
    {
        ActorUserId = actorUserId;
        Action = action;
        EntityType = entityType;
        EntityId = entityId;
        StoreId = storeId;
        Details = details;
    }
}
