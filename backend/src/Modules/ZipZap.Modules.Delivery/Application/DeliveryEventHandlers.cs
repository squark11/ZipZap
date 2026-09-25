using Microsoft.EntityFrameworkCore;
using ZipZap.BuildingBlocks.Messaging;
using ZipZap.Contracts.Ordering;
using ZipZap.Modules.Delivery.Infrastructure;

namespace ZipZap.Modules.Delivery.Application;

/// <summary>
/// Gdy zamówienie jest gotowe do odbioru, tworzy dostawę w puli dostępnych.
/// Dalej dostawę prowadzi kierowca (Accept → PickUp → Deliver), a Delivery
/// PRODUKUJE zdarzenia OrderPickedUp / OrderDelivered.
/// </summary>
public sealed class DeliveryEventHandlers : IIntegrationEventHandler<OrderReadyForPickup>
{
    private readonly DeliveryDbContext _db;

    public DeliveryEventHandlers(DeliveryDbContext db) => _db = db;

    public async Task HandleAsync(OrderReadyForPickup e, CancellationToken ct = default)
    {
        if (await _db.Deliveries.AnyAsync(d => d.OrderId == e.OrderId, ct)) return; // idempotencja
        // Okno dostawy wybrane przez klienta — migawka do planowania (nie zmieniamy jej tutaj).
        _db.Deliveries.Add(new Domain.Delivery(e.OrderId, e.StoreId, e.DeliveryDate, e.WindowStart, e.WindowEnd));
        await _db.SaveChangesAsync(ct);
    }
}
