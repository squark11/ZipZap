using ZipZap.BuildingBlocks.Messaging;

namespace ZipZap.Contracts.Ordering;

// Published language modułu Ordering. Konsumenci (Payments, Delivery, Notifications)
// będą reagować na te zdarzenia bez zależności od wnętrza Ordering.

public sealed record OrderPlaced(
    Guid OrderId, Guid StoreId, Guid CustomerId,
    decimal Subtotal, decimal CommissionAmount, decimal DeliveryFee, decimal Total,
    Guid TimeSlotId, string PaymentMode = PaymentModes.Online) : IntegrationEvent;

/// <summary>
/// Zamówienie gotowe do wydania kierowcy. Okno dostawy wybrane przez klienta (data + godziny lokalne) idzie
/// jako migawka do planowania dostaw — to NIE są dane osobowe (adres/telefon zostają w Ordering).
/// Pola opcjonalne: starsze wiadomości bez nich deserializują się do null.
/// </summary>
public sealed record OrderReadyForPickup(
    Guid OrderId, Guid StoreId,
    DateOnly? DeliveryDate = null, TimeOnly? WindowStart = null, TimeOnly? WindowEnd = null) : IntegrationEvent;

public sealed record OrderPickedUp(Guid OrderId, Guid StoreId) : IntegrationEvent;

public sealed record OrderDelivered(Guid OrderId, Guid StoreId) : IntegrationEvent;

public sealed record OrderCancelled(Guid OrderId, Guid StoreId) : IntegrationEvent;
