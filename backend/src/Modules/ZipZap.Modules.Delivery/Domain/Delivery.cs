using ZipZap.BuildingBlocks.Domain;

namespace ZipZap.Modules.Delivery.Domain;

public enum DeliveryStatus { AvailableForPickup, Assigned, InTransit, Delivered }

public sealed class DeliveryDomainException : Exception
{
    public DeliveryDomainException(string message) : base(message) { }
}

/// <summary>Dostawa — własność kierowcy po zaakceptowaniu. Reaguje na cykl zamówienia.</summary>
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

    /// <summary>Kierowca akceptuje dostawę z puli (tylko dostępną).</summary>
    public void Accept(Guid driverId)
    {
        if (Status != DeliveryStatus.AvailableForPickup)
            throw new DeliveryDomainException("Dostawa nie jest już dostępna do przyjęcia.");
        DriverId = driverId;
        Status = DeliveryStatus.Assigned;
        AssignedAtUtc = DateTime.UtcNow;
    }

    public void MarkPickedUp()
    {
        if (Status != DeliveryStatus.Assigned)
            throw new DeliveryDomainException("Dostawę można odebrać dopiero po przyjęciu.");
        Status = DeliveryStatus.InTransit;
        PickedUpAtUtc = DateTime.UtcNow;
    }

    public void MarkDelivered()
    {
        if (Status != DeliveryStatus.InTransit)
            throw new DeliveryDomainException("Dostawę można oznaczyć jako dostarczoną dopiero w trakcie dostawy.");
        Status = DeliveryStatus.Delivered;
        DeliveredAtUtc = DateTime.UtcNow;
    }

    public bool IsOwnedBy(Guid driverId) => DriverId == driverId;
}
