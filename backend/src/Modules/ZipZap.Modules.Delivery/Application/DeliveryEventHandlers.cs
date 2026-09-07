using Microsoft.EntityFrameworkCore;
using ZipZap.BuildingBlocks.Messaging;
using ZipZap.Contracts.Ordering;
using ZipZap.Modules.Delivery.Infrastructure;

namespace ZipZap.Modules.Delivery.Application;

/// <summary>
/// Szkielet dostaw: projekcja cyklu życia zamówienia do tabeli `deliveries`.
/// (Realne przypisania kierowców / trasy = przyszła implementacja.)
/// </summary>
public sealed class DeliveryEventHandlers :
    IIntegrationEventHandler<OrderReadyForPickup>,
    IIntegrationEventHandler<OrderPickedUp>,
    IIntegrationEventHandler<OrderDelivered>
{
    private readonly DeliveryDbContext _db;

    public DeliveryEventHandlers(DeliveryDbContext db) => _db = db;

    public async Task HandleAsync(OrderReadyForPickup e, CancellationToken ct = default)
    {
        if (await _db.Deliveries.AnyAsync(d => d.OrderId == e.OrderId, ct)) return;
        _db.Deliveries.Add(new Domain.Delivery(e.OrderId, e.StoreId));
        await _db.SaveChangesAsync(ct);
    }

    public async Task HandleAsync(OrderPickedUp e, CancellationToken ct = default)
    {
        var delivery = await _db.Deliveries.FirstOrDefaultAsync(d => d.OrderId == e.OrderId, ct);
        if (delivery is null) return;
        delivery.MarkInTransit();
        await _db.SaveChangesAsync(ct);
    }

    public async Task HandleAsync(OrderDelivered e, CancellationToken ct = default)
    {
        var delivery = await _db.Deliveries.FirstOrDefaultAsync(d => d.OrderId == e.OrderId, ct);
        if (delivery is null) return;
        delivery.MarkDelivered();
        await _db.SaveChangesAsync(ct);
    }
}
