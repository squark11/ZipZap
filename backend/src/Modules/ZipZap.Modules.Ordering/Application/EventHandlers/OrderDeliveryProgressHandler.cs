using Microsoft.EntityFrameworkCore;
using ZipZap.BuildingBlocks.Messaging;
using ZipZap.Contracts.Ordering;
using ZipZap.Modules.Ordering.Domain;
using ZipZap.Modules.Ordering.Infrastructure;

namespace ZipZap.Modules.Ordering.Application.EventHandlers;

/// <summary>
/// Zdarzenia dostawy (produkowane przez moduł Delivery) przesuwają stan zamówienia.
/// Idempotentne: pomijają, jeśli zamówienie nie jest w oczekiwanym stanie.
/// </summary>
public sealed class OrderDeliveryProgressHandler :
    IIntegrationEventHandler<OrderPickedUp>,
    IIntegrationEventHandler<OrderDelivered>
{
    private readonly OrderingDbContext _db;

    public OrderDeliveryProgressHandler(OrderingDbContext db) => _db = db;

    public async Task HandleAsync(OrderPickedUp e, CancellationToken ct = default)
    {
        var order = await _db.Orders.Include(o => o.History).FirstOrDefaultAsync(o => o.Id == e.OrderId, ct);
        if (order is null || order.Status != OrderStatus.ReadyForPickup) return;
        order.PickUp();
        await _db.SaveChangesAsync(ct);
    }

    public async Task HandleAsync(OrderDelivered e, CancellationToken ct = default)
    {
        var order = await _db.Orders.Include(o => o.History).FirstOrDefaultAsync(o => o.Id == e.OrderId, ct);
        if (order is null || order.Status != OrderStatus.InDelivery) return;
        order.MarkDelivered();
        await _db.SaveChangesAsync(ct);
    }
}
