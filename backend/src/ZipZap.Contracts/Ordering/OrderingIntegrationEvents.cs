using ZipZap.BuildingBlocks.Messaging;

namespace ZipZap.Contracts.Ordering;

// Published language modułu Ordering. Konsumenci (Payments, Delivery, Notifications)
// będą reagować na te zdarzenia bez zależności od wnętrza Ordering.

public sealed record OrderPlaced(
    Guid OrderId, Guid StoreId, Guid CustomerId,
    decimal Subtotal, decimal CommissionAmount, decimal DeliveryFee, decimal Total,
    Guid TimeSlotId) : IntegrationEvent;

public sealed record OrderReadyForPickup(Guid OrderId, Guid StoreId) : IntegrationEvent;

public sealed record OrderPickedUp(Guid OrderId, Guid StoreId) : IntegrationEvent;

public sealed record OrderDelivered(Guid OrderId, Guid StoreId) : IntegrationEvent;

public sealed record OrderCancelled(Guid OrderId, Guid StoreId) : IntegrationEvent;
