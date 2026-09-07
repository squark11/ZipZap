using ZipZap.BuildingBlocks.Domain;

namespace ZipZap.Modules.Delivery.Domain;

public enum DeliveryStatus { AvailableForPickup, Assigned, InTransit, Delivered }

/// <summary>Dostawa (szkielet) — projekcja cyklu życia zamówienia dla kierowcy.</summary>
public sealed class Delivery : Entity
{
    public Guid OrderId { get; private set; }
    public Guid StoreId { get; private set; }
    public Guid? DriverId { get; private set; }
    public DeliveryStatus Status { get; private set; }
    public DateTime CreatedAtUtc { get; private set; } = DateTime.UtcNow;
    public DateTime? AssignedAtUtc { get; private set; }
    public DateTime? PickedUpAtUtc { get; private set; }
    public DateTime? DeliveredAtUtc { get; private set; }

    private Delivery() { } // EF

    public Delivery(Guid orderId, Guid storeId)
    {
        OrderId = orderId;
        StoreId = storeId;
        Status = DeliveryStatus.AvailableForPickup;
    }

    public void Assign(Guid driverId)
    {
        DriverId = driverId;
        Status = DeliveryStatus.Assigned;
        AssignedAtUtc = DateTime.UtcNow;
    }

    public void MarkInTransit()
    {
        Status = DeliveryStatus.InTransit;
        PickedUpAtUtc = DateTime.UtcNow;
    }

    public void MarkDelivered()
    {
        Status = DeliveryStatus.Delivered;
        DeliveredAtUtc = DateTime.UtcNow;
    }
}
