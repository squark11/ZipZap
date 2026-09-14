using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using ZipZap.Modules.Ordering.Domain;
using ZipZap.Modules.Ordering.Domain.ReadModel;
using ZipZap.Modules.Ordering.Infrastructure;

namespace ZipZap.Api.IntegrationTests;

/// <summary>
/// Bramka zgód przy checkoucie. Scenariusz seedujemy bezpośrednio w read-modelu Ordering
/// (syntetyczny sklep — bez zależności od asynchronicznej projekcji z Catalog), a politykę
/// prawną ustawiamy adminem przez publiczne API. Dzięki temu test jest deterministyczny.
/// </summary>
[Collection("api")]
public sealed class CheckoutConsentTests
{
    private readonly ApiFactory _f;
    public CheckoutConsentTests(ApiFactory f) => _f = f;

    private sealed record Seeded(Guid CartId, string CartToken, Guid ZoneId, Guid SlotId);

    private async Task<Seeded> SeedCheckoutAsync(Guid storeId, Guid customerId)
    {
        using var scope = _f.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<OrderingDbContext>();

        db.CatalogStores.Add(new CatalogStoreView
        {
            Id = storeId, Name = "Sklep testowy", CommissionRate = 0.10m,
            MinimumOrderValue = 0m, IsActive = true, Status = "Open",
        });
        var productId = Guid.NewGuid();
        db.CatalogProducts.Add(new CatalogProductView
        {
            Id = productId, StoreId = storeId, Name = "Produkt", Price = 10.00m, IsAvailable = true,
        });
        var zone = new DeliveryZone(storeId, "Strefa", 5.00m);
        db.DeliveryZones.Add(zone);
        var slot = new TimeSlot(storeId, zone.Id, DateOnly.FromDateTime(DateTime.UtcNow.Date.AddDays(1)),
            new TimeOnly(10, 0), new TimeOnly(12, 0), maxOrders: 10);
        db.TimeSlots.Add(slot);

        var cart = Cart.Create(storeId, customerId);
        cart.AddItem(productId, "Produkt", 10.00m, 1);
        db.Carts.Add(cart);

        await db.SaveChangesAsync();
        return new Seeded(cart.Id, cart.CartToken, zone.Id, slot.Id);
    }

    private static object CheckoutBody(Seeded s, bool consent) => new
    {
        token = s.CartToken,
        deliveryZoneId = s.ZoneId,
        timeSlotId = s.SlotId,
        deliveryAddress = "ul. Testowa 1, 00-001 Testowo",
        contactPhone = "600100200",
        consentAccepted = consent,
    };

    [Fact]
    public async Task Checkout_blocked_when_store_requires_acceptance_and_not_accepted()
    {
        var admin = await _f.LoginAdminAsync();
        var storeId = Guid.NewGuid();
        // Admin może ustawić dokumenty dowolnego sklepu.
        (await _f.Authed(admin.accessToken).PutAsJsonAsync($"/api/stores/{storeId}/legal",
            new { termsUrl = "https://sklep.pl/regulamin", privacyUrl = "https://sklep.pl/prywatnosc", requiresAcceptance = true }))
            .EnsureSuccessStatusCode();

        var customer = await _f.RegisterCustomerAsync();
        var seeded = await SeedCheckoutAsync(storeId, Guid.Parse(customer.user.id));

        var resp = await _f.Authed(customer.accessToken)
            .PostAsJsonAsync($"/api/ordering/carts/{seeded.CartId}/checkout", CheckoutBody(seeded, consent: false));

        resp.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await resp.Content.ReadAsStringAsync()).Should().Contain("zaakceptuj");
    }

    [Fact]
    public async Task Checkout_succeeds_when_consent_accepted()
    {
        var admin = await _f.LoginAdminAsync();
        var storeId = Guid.NewGuid();
        (await _f.Authed(admin.accessToken).PutAsJsonAsync($"/api/stores/{storeId}/legal",
            new { termsUrl = "https://sklep.pl/regulamin", privacyUrl = "https://sklep.pl/prywatnosc", requiresAcceptance = true }))
            .EnsureSuccessStatusCode();

        var customer = await _f.RegisterCustomerAsync();
        var seeded = await SeedCheckoutAsync(storeId, Guid.Parse(customer.user.id));

        var resp = await _f.Authed(customer.accessToken)
            .PostAsJsonAsync($"/api/ordering/carts/{seeded.CartId}/checkout", CheckoutBody(seeded, consent: true));

        resp.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Checkout_not_blocked_when_store_has_no_policy()
    {
        var customer = await _f.RegisterCustomerAsync();
        var storeId = Guid.NewGuid(); // brak konfiguracji dokumentów dla tego sklepu
        var seeded = await SeedCheckoutAsync(storeId, Guid.Parse(customer.user.id));

        var resp = await _f.Authed(customer.accessToken)
            .PostAsJsonAsync($"/api/ordering/carts/{seeded.CartId}/checkout", CheckoutBody(seeded, consent: false));

        resp.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
