using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using ZipZap.Modules.Delivery.Infrastructure;
using ZipZap.Modules.Ordering.Domain.ReadModel;
using ZipZap.Modules.Ordering.Infrastructure;
using DeliveryEntity = ZipZap.Modules.Delivery.Domain.Delivery;

namespace ZipZap.Api.IntegrationTests;

/// <summary>
/// Izolacja danych klienta (telefon, adres) i dostaw: dostęp mają właściciel zamówienia,
/// obsługa DANEGO sklepu (każdej z jego lokalizacji), przypisany kierowca i admin.
/// </summary>
[Collection("api")]
public sealed class SecurityIsolationTests
{
    private readonly ApiFactory _f;
    public SecurityIsolationTests(ApiFactory f) => _f = f;

    private sealed record IdDto(Guid id);
    private sealed record CartDto(Guid id, string cartToken);

    private sealed record PlacedOrder(string AdminToken, Guid StoreId, Guid OrderId, AuthDto Customer);

    /// <summary>Sklep + read-model Ordering + strefa + termin → klient składa zamówienie.</summary>
    private async Task<PlacedOrder> PlaceOrderAsync()
    {
        var admin = await _f.LoginAdminAsync();
        var ac = _f.Authed(admin.accessToken);
        var storeId = Guid.Parse(await _f.CreateStoreAsync(admin.accessToken));

        Guid productId;
        using (var scope = _f.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<OrderingDbContext>();
            var view = await db.CatalogStores.FindAsync(storeId);
            if (view is null)
                db.CatalogStores.Add(view = new CatalogStoreView { Id = storeId });
            view.Name = "Sklep IT"; view.CommissionRate = 0.10m; view.MinimumOrderValue = 0m;
            view.IsActive = true; view.Status = "Open";
            productId = Guid.NewGuid();
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
            deliveryZoneId = zone.id, date = DateTime.UtcNow.Date.AddDays(2).ToString("yyyy-MM-dd"),
            startTime = "10:00:00", endTime = "12:00:00", maxOrders = 20,
        });
        slotResp.EnsureSuccessStatusCode();
        var slotId = (await slotResp.Content.ReadFromJsonAsync<IdDto>())!.id;

        var customer = await _f.RegisterCustomerAsync();
        var cc = _f.Authed(customer.accessToken);
        var cart = (await (await cc.PostAsJsonAsync("/api/ordering/carts", new { storeId }))
            .Content.ReadFromJsonAsync<CartDto>())!;
        (await cc.PostAsJsonAsync($"/api/ordering/carts/{cart.id}/items?token={cart.cartToken}",
            new { productId, quantity = 1 })).EnsureSuccessStatusCode();
        var checkout = await cc.PostAsJsonAsync($"/api/ordering/carts/{cart.id}/checkout", new
        {
            token = cart.cartToken, deliveryZoneId = zone.id, timeSlotId = slotId,
            deliveryAddress = "ul. Prywatna 7, 30-001 Kraków", contactPhone = "600700800", consentAccepted = true,
        });
        checkout.EnsureSuccessStatusCode();
        var order = (await checkout.Content.ReadFromJsonAsync<IdDto>())!;
        return new PlacedOrder(admin.accessToken, storeId, order.id, customer);
    }

    /// <summary>Deterministycznie tworzy dostawę (bez czekania na asynchroniczny outbox).</summary>
    private Guid SeedDelivery(Guid orderId, Guid storeId)
    {
        using var scope = _f.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DeliveryDbContext>();
        var d = new DeliveryEntity(orderId, storeId);
        db.Deliveries.Add(d);
        db.SaveChanges();
        return d.Id;
    }

    [Fact]
    public async Task Driver_cannot_read_customer_order_or_unassigned_delivery()
    {
        var o = await PlaceOrderAsync();
        var d1 = await _f.CreateStaffAsync(o.AdminToken, o.StoreId.ToString(), "Driver");
        var d2 = await _f.CreateStaffAsync(o.AdminToken, o.StoreId.ToString(), "Driver");
        var d1c = _f.Authed(d1.accessToken);

        // Zamówienie z telefonem i adresem — kierowca (nawet przypisany do sklepu) nie ma dostępu.
        (await d1c.GetAsync($"/api/ordering/orders/{o.OrderId}")).StatusCode.Should().Be(HttpStatusCode.Forbidden);

        // Dostawa nieprzypisana: przypisanie kierowcy do sklepu NIE daje wglądu w cudze dostawy.
        var deliveryId = SeedDelivery(o.OrderId, o.StoreId);
        (await d1c.GetAsync($"/api/delivery/orders/{o.OrderId}")).StatusCode.Should().Be(HttpStatusCode.Forbidden);

        // Po przyjęciu: przypisany kierowca widzi SWOJĄ dostawę, inny kierowca nadal nie.
        (await d1c.PostAsync($"/api/delivery/{deliveryId}/accept", null)).EnsureSuccessStatusCode();
        (await d1c.GetAsync($"/api/delivery/orders/{o.OrderId}")).StatusCode.Should().Be(HttpStatusCode.OK);
        (await _f.Authed(d2.accessToken).GetAsync($"/api/delivery/orders/{o.OrderId}"))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);

        // Kierowca nie ma operacyjnego widoku wszystkich dostaw sklepu.
        (await d1c.GetAsync($"/api/delivery/stores/{o.StoreId}/deliveries")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Driver_token_does_not_carry_store_management_claim()
    {
        var admin = await _f.LoginAdminAsync();
        var storeId = await _f.CreateStoreAsync(admin.accessToken);
        var driver = await _f.CreateStaffAsync(admin.accessToken, storeId, "Driver");

        // Kierowca przypisany do sklepu nie może zarządzać jego ofertą.
        var resp = await _f.Authed(driver.accessToken).PostAsJsonAsync(
            $"/api/catalog/stores/{storeId}/categories", new { name = "X", sortOrder = 0, parentId = (Guid?)null });
        resp.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Order_and_delivery_visible_only_to_owner_store_staff_and_admin()
    {
        var o = await PlaceOrderAsync();
        SeedDelivery(o.OrderId, o.StoreId);
        var emp = await _f.CreateEmployeeAsync(o.AdminToken, o.StoreId.ToString());
        var otherCustomer = await _f.RegisterCustomerAsync();
        var otherStore = await _f.CreateStoreAsync(o.AdminToken);
        var otherEmp = await _f.CreateEmployeeAsync(o.AdminToken, otherStore);

        async Task<HttpStatusCode> Get(string token, string path) => (await _f.Authed(token).GetAsync(path)).StatusCode;
        var orderPath = $"/api/ordering/orders/{o.OrderId}";
        var deliveryPath = $"/api/delivery/orders/{o.OrderId}";

        (await Get(o.Customer.accessToken, orderPath)).Should().Be(HttpStatusCode.OK);
        (await Get(emp.accessToken, orderPath)).Should().Be(HttpStatusCode.OK);
        (await Get(o.AdminToken, orderPath)).Should().Be(HttpStatusCode.OK);
        (await Get(otherCustomer.accessToken, orderPath)).Should().Be(HttpStatusCode.Forbidden);
        (await Get(otherEmp.accessToken, orderPath)).Should().Be(HttpStatusCode.Forbidden);

        (await Get(emp.accessToken, deliveryPath)).Should().Be(HttpStatusCode.OK);
        (await Get(o.AdminToken, deliveryPath)).Should().Be(HttpStatusCode.OK);
        (await Get(otherCustomer.accessToken, deliveryPath)).Should().Be(HttpStatusCode.Forbidden);
        (await Get(otherEmp.accessToken, deliveryPath)).Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Employee_sees_deliveries_of_every_assigned_location_not_only_the_first()
    {
        var admin = await _f.LoginAdminAsync();
        var store1 = await _f.CreateStoreAsync(admin.accessToken);
        var emp = await _f.CreateEmployeeAsync(admin.accessToken, store1);

        var add = await _f.Authed(emp.accessToken).PostAsJsonAsync("/api/merchant/stores",
            new { name = $"Lok2 {Guid.NewGuid():N}".Substring(0, 12), city = "Kraków", commissionRate = 0.10m, minimumOrderValue = 0m });
        add.EnsureSuccessStatusCode();
        var store2 = (await add.Content.ReadFromJsonAsync<IdDto>())!.id;

        var refreshed = (await (await _f.Anon().PostAsJsonAsync("/api/identity/refresh",
            new { refreshToken = emp.refreshToken })).Content.ReadFromJsonAsync<AuthDto>())!;
        var ec = _f.Authed(refreshed.accessToken);

        SeedDelivery(Guid.NewGuid(), Guid.Parse(store1));
        SeedDelivery(Guid.NewGuid(), store2);

        foreach (var sid in new[] { store1, store2.ToString() })
        {
            var resp = await ec.GetAsync($"/api/delivery/stores/{sid}/deliveries");
            resp.StatusCode.Should().Be(HttpStatusCode.OK, "pracownik zarządza lokalizacją {0}", sid);
            (await resp.Content.ReadFromJsonAsync<List<IdDto>>())!.Should().NotBeEmpty();
        }
    }
}
