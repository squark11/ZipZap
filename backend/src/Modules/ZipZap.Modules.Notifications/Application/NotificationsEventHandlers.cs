using System.Text.Json;
using ZipZap.BuildingBlocks.Messaging;
using ZipZap.Contracts.Identity;
using ZipZap.Contracts.Ordering;
using ZipZap.Contracts.Payments;
using ZipZap.Modules.Notifications.Domain;
using ZipZap.Modules.Notifications.Infrastructure;

namespace ZipZap.Modules.Notifications.Application;

/// <summary>
/// Konsument zdarzeń z wielu modułów → zapis powiadomienia + „wysyłka" mockowym kanałem.
/// Demonstruje choreografię zdarzeniową modularnego monolitu.
/// </summary>
public sealed class NotificationsEventHandlers :
    IIntegrationEventHandler<CustomerRegistered>,
    IIntegrationEventHandler<OrderPlaced>,
    IIntegrationEventHandler<PaymentAuthorized>,
    IIntegrationEventHandler<OrderReadyForPickup>,
    IIntegrationEventHandler<OrderDelivered>
{
    private readonly NotificationsDbContext _db;
    private readonly INotificationChannel _channel;

    public NotificationsEventHandlers(NotificationsDbContext db, INotificationChannel channel)
    {
        _db = db;
        _channel = channel;
    }

    public Task HandleAsync(CustomerRegistered e, CancellationToken ct = default)
        => NotifyAsync(e.UserId, "customer.welcome", new { e.Email, e.FullName }, ct);

    public Task HandleAsync(OrderPlaced e, CancellationToken ct = default)
        => NotifyAsync(null, "order.placed", new { e.OrderId, e.StoreId, e.Total }, ct);

    public Task HandleAsync(PaymentAuthorized e, CancellationToken ct = default)
        => NotifyAsync(null, "payment.authorized", new { e.OrderId, e.Amount }, ct);

    public Task HandleAsync(OrderReadyForPickup e, CancellationToken ct = default)
        => NotifyAsync(null, "order.ready", new { e.OrderId, e.StoreId }, ct);

    public Task HandleAsync(OrderDelivered e, CancellationToken ct = default)
        => NotifyAsync(null, "order.delivered", new { e.OrderId, e.StoreId }, ct);

    private async Task NotifyAsync(Guid? recipient, string template, object payload, CancellationToken ct)
    {
        var notification = new Notification(recipient, _channel.Name, template, JsonSerializer.Serialize(payload));
        _db.Notifications.Add(notification);
        await _channel.SendAsync(notification, ct);
        notification.MarkSent();
        await _db.SaveChangesAsync(ct);
    }
}
