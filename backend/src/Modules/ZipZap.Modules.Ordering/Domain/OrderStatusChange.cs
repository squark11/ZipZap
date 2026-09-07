using ZipZap.BuildingBlocks.Domain;

namespace ZipZap.Modules.Ordering.Domain;

/// <summary>Wpis historii zmian statusu zamówienia.</summary>
public sealed class OrderStatusChange : Entity
{
    public Guid OrderId { get; private set; }
    public OrderStatus? FromStatus { get; private set; }
    public OrderStatus ToStatus { get; private set; }
    public Guid? ChangedBy { get; private set; }
    public DateTime ChangedAtUtc { get; private set; } = DateTime.UtcNow;

    private OrderStatusChange() { } // EF

    public OrderStatusChange(OrderStatus? fromStatus, OrderStatus toStatus, Guid? changedBy)
    {
        FromStatus = fromStatus;
        ToStatus = toStatus;
        ChangedBy = changedBy;
    }
}
