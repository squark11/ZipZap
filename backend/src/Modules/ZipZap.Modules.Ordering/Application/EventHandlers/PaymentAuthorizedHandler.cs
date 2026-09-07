using Microsoft.EntityFrameworkCore;
using ZipZap.BuildingBlocks.Messaging;
using ZipZap.Contracts.Payments;
using ZipZap.Modules.Ordering.Domain;
using ZipZap.Modules.Ordering.Infrastructure;

namespace ZipZap.Modules.Ordering.Application.EventHandlers;

/// <summary>
/// Po autoryzacji płatności potwierdza zamówienie (Placed → Confirmed).
/// Idempotentny: pomija, jeśli zamówienie nie jest już w stanie Placed.
/// </summary>
public sealed class PaymentAuthorizedHandler : IIntegrationEventHandler<PaymentAuthorized>
{
    private readonly OrderingDbContext _db;

    public PaymentAuthorizedHandler(OrderingDbContext db) => _db = db;

    public async Task HandleAsync(PaymentAuthorized e, CancellationToken ct = default)
    {
        var order = await _db.Orders.Include(o => o.History).FirstOrDefaultAsync(o => o.Id == e.OrderId, ct);
        if (order is null || order.Status != OrderStatus.Placed) return;

        order.Confirm();
        await _db.SaveChangesAsync(ct);
    }
}
