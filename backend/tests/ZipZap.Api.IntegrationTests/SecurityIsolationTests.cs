using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
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

    private sealed record DeliveryRow(Guid id, Guid storeId, Guid? driverId, string status);
    private sealed record OrderStatusDto(Guid id, string status);

    /// <summary>Deterministycznie tworzy dostawę (bez czekania na asynchroniczny outbox).
    /// <paramref name="acceptedBy"/> symuluje dane historyczne (dostawa już przypisana).</summary>
    private Guid SeedDelivery(Guid orderId, Guid storeId, Guid? acceptedBy = null)
    {
        using var scope = _f.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DeliveryDbContext>();
        var d = new DeliveryEntity(orderId, storeId);
        if (acceptedBy is Guid driver) d.Accept(driver);
        db.Deliveries.Add(d);
        db.SaveChanges();
        return d.Id;
    }

    /// <summary>Stan dostawy + liczba zdarzeń modułu Delivery (OrderPickedUp/OrderDelivered) dla zamówienia.</summary>
    private (string Status, Guid? DriverId, int Events) DeliveryState(Guid deliveryId, Guid orderId)
    {
        using var scope = _f.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DeliveryDbContext>();
        var d = db.Deliveries.AsNoTracking().Single(x => x.Id == deliveryId);
        var key = orderId.ToString();
        var events = db.OutboxMessages.AsNoTracking().Count(m => m.Payload.Contains(key));
        return (d.Status.ToString(), d.DriverId, events);
    }

    private async Task<string> OrderStatus(string adminToken, Guid orderId)
        => (await _f.Authed(adminToken).GetFromJsonAsync<OrderStatusDto>($"/api/ordering/orders/{orderId}"))!.status;

    [Fact]
    public async Task Driver_of_store_A_cannot_see_or_act_on_delivery_of_store_B_and_nothing_changes()
    {
        var oB = await PlaceOrderAsync(); // prawdziwe zamówienie w sklepie B
        var storeA = Guid.Parse(await _f.CreateStoreAsync(oB.AdminToken));
        var driverA = await _f.CreateStaffAsync(oB.AdminToken, storeA.ToString(), "Driver");
        var dac = _f.Authed(driverA.accessToken);

        var deliveryB = SeedDelivery(oB.OrderId, oB.StoreId);
        var deliveryA = SeedDelivery(Guid.NewGuid(), storeA);

        // 1) Pula dostępnych: wyłącznie dostawy sklepu A.
        var pool = (await dac.GetFromJsonAsync<List<DeliveryRow>>("/api/delivery/available"))!;
        pool.Should().Contain(d => d.id == deliveryA);
        pool.Should().NotContain(d => d.id == deliveryB);
        pool.Should().OnlyContain(d => d.storeId == storeA);

        // 2) Przyjęcie, odbiór i dostarczenie dostawy sklepu B — odmowa (403 z kontroli zakresu,
        //    nie 409 z maszyny stanów).
        foreach (var action in new[] { "accept", "pick-up", "delivered" })
            (await dac.PostAsync($"/api/delivery/{deliveryB}/{action}", null))
                .StatusCode.Should().Be(HttpStatusCode.Forbidden, "akcja {0}", action);

        // 3) Stan bez zmian: dostawa dostępna i nieprzypisana, brak zdarzeń, zamówienie nadal Placed.
        var state = DeliveryState(deliveryB, oB.OrderId);
        state.Status.Should().Be("AvailableForPickup");
        state.DriverId.Should().BeNull();
        state.Events.Should().Be(0);
        (await OrderStatus(oB.AdminToken, oB.OrderId)).Should().Be("Placed");

        // 4) Kontrola pozytywna: kierowca sklepu B może przyjąć tę dostawę.
        var driverB = await _f.CreateStaffAsync(oB.AdminToken, oB.StoreId.ToString(), "Driver");
        (await _f.Authed(driverB.accessToken).PostAsync($"/api/delivery/{deliveryB}/accept", null))
            .EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task Foreign_store_delivery_assigned_before_the_fix_cannot_be_progressed()
    {
        // Dane historyczne: dostawa sklepu B przypisana kierowcy sklepu A (sprzed poprawki).
        var oB = await PlaceOrderAsync();
        var storeA = await _f.CreateStoreAsync(oB.AdminToken);
        var driverA = await _f.CreateStaffAsync(oB.AdminToken, storeA, "Driver");
        var deliveryB = SeedDelivery(oB.OrderId, oB.StoreId, acceptedBy: Guid.Parse(driverA.user.id));
        var dac = _f.Authed(driverA.accessToken);

        (await dac.PostAsync($"/api/delivery/{deliveryB}/pick-up", null)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await dac.PostAsync($"/api/delivery/{deliveryB}/delivered", null)).StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var state = DeliveryState(deliveryB, oB.OrderId);
        state.Status.Should().Be("Assigned");
        state.Events.Should().Be(0, "nie może powstać OrderPickedUp ani OrderDelivered");
        (await OrderStatus(oB.AdminToken, oB.OrderId)).Should().Be("Placed");
    }

    [Fact]
    public async Task Active_driver_without_store_assignment_sees_nothing_and_accepts_nothing()
    {
        // Admin może nadać rolę Driver bez sklepu (/admin/users/{id}/role) — konto aktywne, bez driver_store_id.
        var admin = await _f.LoginAdminAsync();
        var store = await _f.CreateStoreAsync(admin.accessToken);
        var delivery = SeedDelivery(Guid.NewGuid(), Guid.Parse(store));

        var user = await _f.RegisterCustomerAsync();
        (await _f.Authed(admin.accessToken).PostAsJsonAsync($"/api/identity/admin/users/{user.user.id}/role",
            new { role = "Driver" })).EnsureSuccessStatusCode();
        var driver = await _f.LoginAsync(user.user.email, "Passw0rd!");
        driver.user.roles.Should().Contain("Driver");
        var dc = _f.Authed(driver.accessToken);

        (await dc.GetFromJsonAsync<List<DeliveryRow>>("/api/delivery/available"))!.Should().BeEmpty();
        (await dc.PostAsync($"/api/delivery/{delivery}/accept", null)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        DeliveryState(delivery, Guid.NewGuid()).DriverId.Should().BeNull();
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
