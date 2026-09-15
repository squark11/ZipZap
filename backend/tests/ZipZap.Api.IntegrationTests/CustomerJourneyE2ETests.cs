using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using ZipZap.Modules.Ordering.Domain.ReadModel;
using ZipZap.Modules.Ordering.Infrastructure;

namespace ZipZap.Api.IntegrationTests;

/// <summary>
/// Twarda ścieżka klienta (E2E): rejestracja → sklep (wg odległości) → koszyk → checkout ze zgodą
/// → śledzenie → potwierdzenie przez sklep. Sklep istnieje w Catalog (lista/odległość) i w read‑modelu
/// Ordering (koszyk/checkout) — seedujemy read‑model deterministycznie, bez zależności od projekcji async.
/// </summary>
[Collection("api")]
public sealed class CustomerJourneyE2ETests
{
    private readonly ApiFactory _f;
    public CustomerJourneyE2ETests(ApiFactory f) => _f = f;

    private sealed record Store(Guid id, double? distanceKm);
    private sealed record Cart(Guid id, string cartToken);
    private sealed record Zone(Guid id);
    private sealed record Order(Guid id, string status, decimal total, OrderItem[] items);
    private sealed record OrderItem(string productName, int quantity);

    private Guid SeedOrderingReadModel(Guid storeId)
    {
        using var scope = _f.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<OrderingDbContext>();
        db.CatalogStores.Add(new CatalogStoreView
        {
            Id = storeId, Name = "Sklep E2E", CommissionRate = 0.10m,
            MinimumOrderValue = 0m, IsActive = true, Status = "Open",
        });
        var productId = Guid.NewGuid();
        db.CatalogProducts.Add(new CatalogProductView
        {
            Id = productId, StoreId = storeId, Name = "Chleb żytni", Price = 6.00m, IsAvailable = true,
        });
        db.SaveChanges();
        return productId;
    }

    [Fact]
    public async Task Full_journey_register_browse_by_distance_order_and_track()
    {
        var admin = await _f.LoginAdminAsync();
        var ac = _f.Authed(admin.accessToken);

        // 1) Sklep w Catalog (z współrzędnymi Krakowa) → widoczny na liście wg odległości.
        const double refLat = 50.0614, refLng = 19.9366;
        var createStore = await ac.PostAsJsonAsync("/api/catalog/stores", new
        {
            name = $"E2E {Guid.NewGuid():N}".Substring(0, 12), city = "Kraków",
            commissionRate = 0.10m, minimumOrderValue = 0m, latitude = 50.0620, longitude = 19.9370,
        });
        createStore.EnsureSuccessStatusCode();
        var storeId = (await createStore.Content.ReadFromJsonAsync<Store>())!.id;

        // read‑model Ordering (koszyk/checkout) + produkt
        var productId = SeedOrderingReadModel(storeId);

        // strefa + termin (sklep/admin)
        var zone = (await (await ac.PostAsJsonAsync($"/api/ordering/stores/{storeId}/zones",
            new { name = "Centrum", deliveryFee = 8.00m, postalCodes = (string[]?)null }))
            .Content.ReadFromJsonAsync<Zone>())!;
        var date = DateTime.UtcNow.Date.AddDays(2).ToString("yyyy-MM-dd");
        var slotResp = await ac.PostAsJsonAsync($"/api/ordering/stores/{storeId}/slots",
            new { deliveryZoneId = zone.id, date, startTime = "10:00:00", endTime = "12:00:00", maxOrders = 20 });
        slotResp.EnsureSuccessStatusCode();
        var slotId = (await slotResp.Content.ReadFromJsonAsync<Dictionary<string, object>>())!["id"].ToString();

        // 2) Klient się rejestruje.
        var customer = await _f.RegisterCustomerAsync();
        var cc = _f.Authed(customer.accessToken);

        // 3) Klient widzi sklep wg odległości.
        var nearby = await _f.Anon().GetFromJsonAsync<Store[]>(
            $"/api/catalog/stores?lat={refLat.ToString(System.Globalization.CultureInfo.InvariantCulture)}&lng={refLng.ToString(System.Globalization.CultureInfo.InvariantCulture)}");
        nearby!.Single(s => s.id == storeId).distanceKm.Should().NotBeNull();

        // 4) Koszyk → dodaj produkt.
        var cart = (await (await cc.PostAsJsonAsync("/api/ordering/carts", new { storeId }))
            .Content.ReadFromJsonAsync<Cart>())!;
        (await cc.PostAsJsonAsync($"/api/ordering/carts/{cart.id}/items?token={cart.cartToken}",
            new { productId, quantity = 2 })).EnsureSuccessStatusCode();

        // 5) Checkout (ze zgodą — sklep nie wymaga, ale przekazujemy).
        var checkout = await cc.PostAsJsonAsync($"/api/ordering/carts/{cart.id}/checkout", new
        {
            token = cart.cartToken, deliveryZoneId = zone.id, timeSlotId = slotId,
            deliveryAddress = "ul. Testowa 1, 30-001 Kraków", contactPhone = "600100200", consentAccepted = true,
        });
        checkout.EnsureSuccessStatusCode();
        var order = (await checkout.Content.ReadFromJsonAsync<Order>())!;
        order.status.Should().Be("Placed");
        order.items.Should().ContainSingle(i => i.productName == "Chleb żytni" && i.quantity == 2);
        order.total.Should().Be(20.00m); // 2×6 + 8 dostawa

        // 6) Klient śledzi zamówienie.
        (await cc.GetFromJsonAsync<Order>($"/api/ordering/orders/{order.id}"))!.status.Should().Be("Placed");

        // 7) Sklep potwierdza → klient widzi „Confirmed".
        (await ac.PostAsync($"/api/ordering/orders/{order.id}/confirm", null)).EnsureSuccessStatusCode();
        (await cc.GetFromJsonAsync<Order>($"/api/ordering/orders/{order.id}"))!.status.Should().Be("Confirmed");
    }
}
