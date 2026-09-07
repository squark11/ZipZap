using Microsoft.EntityFrameworkCore;
using ZipZap.BuildingBlocks.Messaging;
using ZipZap.BuildingBlocks.Outbox;
using ZipZap.Contracts.Ordering;
using ZipZap.Contracts.Payments;
using ZipZap.Modules.Payments.Domain;
using ZipZap.Modules.Payments.Infrastructure;

namespace ZipZap.Modules.Payments.Application;

/// <summary>
/// Szkielet płatności: mockowa bramka autoryzuje płatność po złożeniu zamówienia
/// i księguje prowizję po dostawie. Realny provider = przyszły adapter.
/// </summary>
public sealed class PaymentsEventHandlers :
    IIntegrationEventHandler<OrderPlaced>,
    IIntegrationEventHandler<OrderDelivered>
{
    private readonly PaymentsDbContext _db;
    private readonly IIntegrationEventTypeRegistry _events;

    public PaymentsEventHandlers(PaymentsDbContext db, IIntegrationEventTypeRegistry events)
    {
        _db = db;
        _events = events;
    }

    public async Task HandleAsync(OrderPlaced e, CancellationToken ct = default)
    {
        if (await _db.Payments.AnyAsync(p => p.OrderId == e.OrderId, ct)) return; // idempotencja

        var payment = new Payment(e.OrderId, e.StoreId, e.Total, e.CommissionAmount);
        payment.Authorize($"MOCK-{Guid.NewGuid():N}"); // mock: bramka od razu autoryzuje
        _db.Payments.Add(payment);

        _db.AddOutboxMessage(new PaymentAuthorized(e.OrderId, e.StoreId, e.Total), _events);
        await _db.SaveChangesAsync(ct);
    }

    public async Task HandleAsync(OrderDelivered e, CancellationToken ct = default)
    {
        var payment = await _db.Payments.FirstOrDefaultAsync(p => p.OrderId == e.OrderId, ct);
        if (payment is null) return;
        if (await _db.CommissionLedger.AnyAsync(l => l.OrderId == e.OrderId, ct)) return; // idempotencja

        _db.CommissionLedger.Add(new CommissionLedgerEntry(e.StoreId, e.OrderId, payment.CommissionAmount));
        payment.Settle();
        await _db.SaveChangesAsync(ct);
    }
}
