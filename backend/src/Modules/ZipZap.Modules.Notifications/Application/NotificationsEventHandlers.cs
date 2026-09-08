using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using ZipZap.BuildingBlocks.Messaging;
using ZipZap.Contracts.Identity;
using ZipZap.Contracts.Ordering;
using ZipZap.Contracts.Payments;
using ZipZap.Modules.Notifications.Domain;
using ZipZap.Modules.Notifications.Infrastructure;

namespace ZipZap.Modules.Notifications.Application;

/// <summary>
/// Konsument zdarzeń z wielu modułów. Rozwiązuje odbiorcę (klienta danego
/// zamówienia z lokalnego read-modelu), zapisuje powiadomienie in-app i wysyła
/// push (mock) na zarejestrowane urządzenia użytkownika.
/// </summary>
public sealed class NotificationsEventHandlers :
    IIntegrationEventHandler<CustomerRegistered>,
    IIntegrationEventHandler<OrderPlaced>,
    IIntegrationEventHandler<PaymentAuthorized>,
    IIntegrationEventHandler<OrderReadyForPickup>,
    IIntegrationEventHandler<OrderPickedUp>,
    IIntegrationEventHandler<OrderDelivered>
{
    private readonly NotificationsDbContext _db;
    private readonly INotificationChannel _channel;
    private readonly IPushSender _push;

    public NotificationsEventHandlers(NotificationsDbContext db, INotificationChannel channel, IPushSender push)
    {
        _db = db;
        _channel = channel;
        _push = push;
    }

    public Task HandleAsync(CustomerRegistered e, CancellationToken ct = default)
        => NotifyAsync(e.UserId, "customer.welcome", "Witaj w ZipZap", "Twoje konto zostało utworzone.",
            new { e.Email, e.FullName }, ct);

    public async Task HandleAsync(OrderPlaced e, CancellationToken ct = default)
    {
        // Zapamiętaj odbiorcę zamówienia do kolejnych powiadomień.
        if (!await _db.OrderRecipients.AnyAsync(r => r.OrderId == e.OrderId, ct))
            _db.OrderRecipients.Add(new OrderRecipient { OrderId = e.OrderId, CustomerId = e.CustomerId });

        await NotifyAsync(e.CustomerId, "order.placed", "Zamówienie złożone",
            $"Zamówienie na {e.Total:0.00} zł zostało złożone. Oczekuje na płatność.", new { e.OrderId, e.Total }, ct);
    }

    public async Task HandleAsync(PaymentAuthorized e, CancellationToken ct = default)
        => await NotifyOrderAsync(e.OrderId, "payment.authorized", "Płatność potwierdzona",
            "Otrzymaliśmy płatność — sklep przygotuje Twoje zamówienie.", new { e.OrderId, e.Amount }, ct);

    public async Task HandleAsync(OrderReadyForPickup e, CancellationToken ct = default)
        => await NotifyOrderAsync(e.OrderId, "order.ready", "Zamówienie gotowe",
            "Zamówienie czeka na kierowcę.", new { e.OrderId }, ct);

    public async Task HandleAsync(OrderPickedUp e, CancellationToken ct = default)
        => await NotifyOrderAsync(e.OrderId, "order.in_delivery", "Zamówienie w dostawie",
            "Kierowca odebrał Twoje zamówienie i jest w drodze.", new { e.OrderId }, ct);

    public async Task HandleAsync(OrderDelivered e, CancellationToken ct = default)
        => await NotifyOrderAsync(e.OrderId, "order.delivered", "Zamówienie dostarczone",
            "Twoje zamówienie zostało dostarczone. Smacznego!", new { e.OrderId }, ct);

    private async Task NotifyOrderAsync(Guid orderId, string template, string title, string body, object payload, CancellationToken ct)
    {
        var recipient = await _db.OrderRecipients.Where(r => r.OrderId == orderId)
            .Select(r => (Guid?)r.CustomerId).FirstOrDefaultAsync(ct);
        await NotifyAsync(recipient, template, title, body, payload, ct);
    }

    private async Task NotifyAsync(Guid? recipient, string template, string title, string body, object payload, CancellationToken ct)
    {
        var notification = new Notification(recipient, _channel.Name, template, JsonSerializer.Serialize(payload));
        _db.Notifications.Add(notification);
        await _channel.SendAsync(notification, ct);
        notification.MarkSent();

        if (recipient is Guid userId)
        {
            var tokens = await _db.DeviceTokens.Where(t => t.UserId == userId).Select(t => t.Token).ToListAsync(ct);
            await _push.SendAsync(new PushMessage(userId, title, body, tokens), ct);
        }

        await _db.SaveChangesAsync(ct);
    }
}
