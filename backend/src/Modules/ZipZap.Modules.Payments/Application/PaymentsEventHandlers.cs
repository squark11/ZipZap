using Microsoft.EntityFrameworkCore;
using ZipZap.BuildingBlocks.Messaging;
using ZipZap.Contracts.Ordering;
using ZipZap.Modules.Payments.Domain;
using ZipZap.Modules.Payments.Infrastructure;

namespace ZipZap.Modules.Payments.Application;

/// <summary>
/// Reakcje płatności na zdarzenia zamówienia:
///  - OrderPlaced → utwórz płatność Pending + sesję u dostawcy (autoryzacja
///    NASTĘPUJE dopiero po zweryfikowanym webhooku, nie tutaj),
///  - OrderDelivered → zaksięguj prowizję i rozlicz płatność.
/// </summary>
public sealed class PaymentsEventHandlers :
    IIntegrationEventHandler<OrderPlaced>,
    IIntegrationEventHandler<OrderDelivered>
{
    private readonly PaymentsDbContext _db;
    private readonly PaymentProviderRegistry _providers;

    public PaymentsEventHandlers(PaymentsDbContext db, PaymentProviderRegistry providers)
    {
        _db = db;
        _providers = providers;
    }

    public async Task HandleAsync(OrderPlaced e, CancellationToken ct = default)
    {
        if (await _db.Payments.AnyAsync(p => p.OrderId == e.OrderId, ct)) return; // idempotencja

        var payment = new Payment(e.OrderId, e.StoreId, e.Total, e.DeliveryFee, e.CommissionAmount);

        var provider = _providers.Default;
        var session = await provider.CreateSessionAsync(
            new PaymentSessionRequest(payment.Id, e.OrderId, e.Total, "PLN", $"ZipZap zamówienie {e.OrderId:N}"), ct);
        payment.AttachSession(provider.Key, session.SessionId, session.RedirectUrl);

        _db.Payments.Add(payment);
        await _db.SaveChangesAsync(ct);
        // Brak PaymentAuthorized — autorytatywnym źródłem jest webhook dostawcy.
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
