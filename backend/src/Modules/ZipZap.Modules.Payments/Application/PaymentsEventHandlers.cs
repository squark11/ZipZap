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
    private readonly IStorePaymentGateway _storeGateway;

    public PaymentsEventHandlers(PaymentsDbContext db, PaymentProviderRegistry providers, IStorePaymentGateway storeGateway)
    {
        _db = db;
        _providers = providers;
        _storeGateway = storeGateway;
    }

    public async Task HandleAsync(OrderPlaced e, CancellationToken ct = default)
    {
        if (await _db.Payments.AnyAsync(p => p.OrderId == e.OrderId, ct)) return; // idempotencja

        var payment = new Payment(e.OrderId, e.StoreId, e.CustomerId, e.Total, e.DeliveryFee, e.CommissionAmount);

        // Per-store bramka: jeśli sklep skonfigurował dostawcę i jest on zarejestrowany,
        // użyj go; inaczej dostawca domyślny (mock) — bezpieczny fallback.
        var key = await _storeGateway.GetProviderKeyAsync(e.StoreId, ct);
        var provider = (key is not null ? _providers.Get(key) : null) ?? _providers.Default;
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
