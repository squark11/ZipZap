using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using ZipZap.Modules.Ordering.Domain.ReadModel;
using ZipZap.Modules.Ordering.Infrastructure;

namespace ZipZap.Api.IntegrationTests;

/// <summary>Wspólny scenariusz: sklep (Catalog + read-model Ordering) ze strefą i terminem → koszyk → checkout.</summary>
internal static class OrderScenario
{
    public sealed record Setup(string AdminToken, Guid StoreId, Guid ZoneId, Guid SlotId, Guid ProductId);
    private sealed record IdDto(Guid id);
    private sealed record CartDto(Guid id, string cartToken);

    /// <param name="slotDate">Data terminu dostawy (lokalna); domyślnie +3 dni — zawsze PO najbliższej rundzie.</param>
    public static async Task<Setup> StoreWithSlotAsync(ApiFactory f, int maxOrders = 20,
        DateOnly? slotDate = null, string startTime = "18:00:00", string endTime = "20:00:00", string storeStatus = "Open")
    {
        var admin = await f.LoginAdminAsync();
        var ac = f.Authed(admin.accessToken);
        var storeId = Guid.Parse(await f.CreateStoreAsync(admin.accessToken));

        var productId = Guid.NewGuid();
        using (var scope = f.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<OrderingDbContext>();
            var view = await db.CatalogStores.FindAsync(storeId);
            if (view is null) db.CatalogStores.Add(view = new CatalogStoreView { Id = storeId });
            view.Name = "Sklep IT"; view.CommissionRate = 0.10m; view.MinimumOrderValue = 0m;
            view.IsActive = true; view.Status = storeStatus;
            db.CatalogProducts.Add(new CatalogProductView
            {
                Id = productId, StoreId = storeId, Name = "Mleko 1l", Price = 4.00m, IsAvailable = true,
            });
            await db.SaveChangesAsync();
        }

        var zone = (await (await ac.PostAsJsonAsync($"/api/ordering/stores/{storeId}/zones",
            new { name = "Centrum", deliveryFee = 8.00m, postalCodes = (string[]?)null }))
            .Content.ReadFromJsonAsync<IdDto>())!;
        var slotResp = await ac.PostAsJsonAsync($"/api/ordering/stores/{storeId}/slots", new
        {
            deliveryZoneId = zone.id,
            date = (slotDate ?? DateOnly.FromDateTime(DateTime.UtcNow.Date.AddDays(3))).ToString("yyyy-MM-dd"),
            startTime, endTime, maxOrders,
        });
        slotResp.EnsureSuccessStatusCode();
        var slotId = (await slotResp.Content.ReadFromJsonAsync<IdDto>())!.id;
        return new Setup(admin.accessToken, storeId, zone.id, slotId, productId);
    }

    /// <summary>Dodatkowy termin dostawy dla istniejącego sklepu/strefy.</summary>
    public static async Task<Guid> AddSlotAsync(ApiFactory f, Setup s, DateOnly date, string startTime, string endTime)
    {
        var resp = await f.Authed(s.AdminToken).PostAsJsonAsync($"/api/ordering/stores/{s.StoreId}/slots", new
        {
            deliveryZoneId = s.ZoneId, date = date.ToString("yyyy-MM-dd"), startTime, endTime, maxOrders = 20,
        });
        resp.EnsureSuccessStatusCode();
        return (await resp.Content.ReadFromJsonAsync<IdDto>())!.id;
    }

    /// <summary>Nowy koszyk z jednym produktem i próba złożenia zamówienia (zwraca surową odpowiedź).</summary>
    public static async Task<HttpResponseMessage> CheckoutAsync(ApiFactory f, Setup s, string customerToken,
        Guid? slotId = null, DateTime? expectedRoundStartsAtUtc = null)
    {
        var cc = f.Authed(customerToken);
        var cart = (await (await cc.PostAsJsonAsync("/api/ordering/carts", new { storeId = s.StoreId }))
            .Content.ReadFromJsonAsync<CartDto>())!;
        (await cc.PostAsJsonAsync($"/api/ordering/carts/{cart.id}/items?token={cart.cartToken}",
            new { productId = s.ProductId, quantity = 1 })).EnsureSuccessStatusCode();
        return await cc.PostAsJsonAsync($"/api/ordering/carts/{cart.id}/checkout", new
        {
            token = cart.cartToken, deliveryZoneId = s.ZoneId, timeSlotId = slotId ?? s.SlotId,
            deliveryAddress = "ul. Testowa 1, 30-001 Kraków", contactPhone = "600100200", consentAccepted = true,
            expectedRoundStartsAtUtc,
        });
    }

    /// <summary>Rejestruje klienta i nadaje mu rolę (np. Tester); zwraca świeży token z nową rolą.</summary>
    public static async Task<AuthDto> CustomerWithRoleAsync(ApiFactory f, string adminToken, string role)
    {
        var user = await f.RegisterCustomerAsync();
        (await f.Authed(adminToken).PostAsJsonAsync($"/api/identity/admin/users/{user.user.id}/role", new { role }))
            .EnsureSuccessStatusCode();
        return await f.LoginAsync(user.user.email, "Passw0rd!");
    }
}
