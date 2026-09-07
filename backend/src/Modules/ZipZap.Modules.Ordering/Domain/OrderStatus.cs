namespace ZipZap.Modules.Ordering.Domain;

/// <summary>
/// Maszyna stanów zamówienia:
/// Placed → Confirmed → Picking → ReadyForPickup → InDelivery → Delivered → Completed;
/// Cancelled dostępny do momentu odbioru przez kierowcę.
/// </summary>
public enum OrderStatus
{
    Placed,
    Confirmed,
    Picking,
    ReadyForPickup,
    InDelivery,
    Delivered,
    Completed,
    Cancelled
}
