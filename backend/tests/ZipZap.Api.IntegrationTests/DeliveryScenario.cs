using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ZipZap.BuildingBlocks.Outbox;
using ZipZap.Modules.Delivery.Infrastructure;

namespace ZipZap.Api.IntegrationTests;

/// <summary>Scenariusz dostawy: zamówienie → potwierdzone → kompletowane → gotowe → dostawa (przez prawdziwe zdarzenia).</summary>
internal static class DeliveryScenario
{
    public sealed record Ready(Guid OrderId, Guid DeliveryId, AuthDto Customer);
    private sealed record IdDto(Guid id);

    public static async Task<Ready> ReadyOrderAsync(ApiFactory f, OrderScenario.Setup s, AuthDto? customer = null)
    {
        customer ??= await f.RegisterCustomerAsync();
        var resp = await OrderScenario.CheckoutAsync(f, s, customer.accessToken);
        resp.EnsureSuccessStatusCode();
        var orderId = (await resp.Content.ReadFromJsonAsync<IdDto>())!.id;
        var admin = f.Authed(s.AdminToken);
        foreach (var step in new[] { "confirm", "start-picking", "ready" })
            (await admin.PostAsync($"/api/ordering/orders/{orderId}/{step}", null)).EnsureSuccessStatusCode();
        return new Ready(orderId, await WaitForDeliveryAsync(f, orderId), customer);
    }

    /// <summary>Przetwarza outboxy wszystkich modułów od razu (bez czekania na cykl dispatchera w tle).</summary>
    public static async Task FlushOutboxAsync(ApiFactory f)
    {
        using var scope = f.Services.CreateScope();
        foreach (var p in scope.ServiceProvider.GetServices<IOutboxProcessor>()) await p.ProcessPendingAsync();
    }

    public static async Task<Guid> WaitForDeliveryAsync(ApiFactory f, Guid orderId)
    {
        for (var i = 0; i < 60; i++)
        {
            await FlushOutboxAsync(f);
            using var scope = f.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<DeliveryDbContext>();
            var d = await db.Deliveries.AsNoTracking().FirstOrDefaultAsync(x => x.OrderId == orderId);
            if (d is not null) return d.Id;
            await Task.Delay(100);
        }
        throw new TimeoutException("Dostawa nie powstała ze zdarzenia OrderReadyForPickup.");
    }

    public static Task<HttpResponseMessage> AssignAsync(ApiFactory f, string token, Guid storeId, Guid deliveryId,
        Guid driverId, int expectedVersion = 0)
        => f.Authed(token).PostAsJsonAsync($"/api/delivery/stores/{storeId}/deliveries/{deliveryId}/assign",
            new { driverId, expectedVersion });

    /// <summary>Liczba zdarzeń danego typu (np. „OrderDelivered") w outboxie modułu dostaw dla zamówienia.</summary>
    public static async Task<int> DeliveryEventsAsync(ApiFactory f, string eventName, Guid orderId)
    {
        using var scope = f.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DeliveryDbContext>();
        var key = orderId.ToString();
        var types = await db.OutboxMessages.AsNoTracking().Where(m => m.Payload.Contains(key)).Select(m => m.Type).ToListAsync();
        return types.Count(t => t.EndsWith("." + eventName));
    }
}
