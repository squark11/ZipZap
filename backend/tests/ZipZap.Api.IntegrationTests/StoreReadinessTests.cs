using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Xunit;

namespace ZipZap.Api.IntegrationTests;

[Collection("api")]
public sealed class StoreReadinessTests
{
    private readonly ApiFactory _f;
    public StoreReadinessTests(ApiFactory f) => _f = f;

    private sealed record Step(string key, bool done, bool required);
    private sealed record Readiness(bool readyToSell, Step[] steps);
    private sealed record Created(Guid id);

    private static bool Done(Readiness r, string key) => r.steps.Single(s => s.key == key).done;

    [Fact]
    public async Task Fresh_store_is_not_ready_without_products_zone_slot()
    {
        var admin = await _f.LoginAdminAsync();
        var storeId = await _f.CreateStoreAsync(admin.accessToken);
        var c = _f.Authed(admin.accessToken);

        var r = (await c.GetFromJsonAsync<Readiness>($"/api/stores/{storeId}/readiness"))!;
        r.readyToSell.Should().BeFalse();
        Done(r, "products").Should().BeFalse();
        Done(r, "zone").Should().BeFalse();
        Done(r, "slot").Should().BeFalse();
        Done(r, "legal").Should().BeTrue();     // brak wymogu akceptacji = ok
        Done(r, "published").Should().BeTrue();  // nowy sklep jest Aktywny+Otwarty
    }

    [Fact]
    public async Task Store_becomes_ready_after_product_zone_and_slot()
    {
        var admin = await _f.LoginAdminAsync();
        var storeId = await _f.CreateStoreAsync(admin.accessToken);
        var c = _f.Authed(admin.accessToken);

        // produkt (dostępny)
        (await c.PostAsJsonAsync($"/api/catalog/stores/{storeId}/products",
            new { name = "Chleb", price = 6.00m, unit = "szt" })).EnsureSuccessStatusCode();

        // strefa dostawy
        var zone = (await (await c.PostAsJsonAsync($"/api/ordering/stores/{storeId}/zones",
            new { name = "Centrum", deliveryFee = 5.00m, postalCodes = (string[]?)null }))
            .Content.ReadFromJsonAsync<Created>())!;

        // termin dostawy (przyszłość)
        var date = DateTime.UtcNow.Date.AddDays(2).ToString("yyyy-MM-dd");
        (await c.PostAsJsonAsync($"/api/ordering/stores/{storeId}/slots",
            new { deliveryZoneId = zone.id, date, startTime = "10:00:00", endTime = "12:00:00", maxOrders = 20 }))
            .EnsureSuccessStatusCode();

        var r = (await c.GetFromJsonAsync<Readiness>($"/api/stores/{storeId}/readiness"))!;
        Done(r, "products").Should().BeTrue();
        Done(r, "zone").Should().BeTrue();
        Done(r, "slot").Should().BeTrue();
        r.readyToSell.Should().BeTrue();
    }

    [Fact]
    public async Task Other_store_employee_cannot_read_readiness()
    {
        var admin = await _f.LoginAdminAsync();
        var storeA = await _f.CreateStoreAsync(admin.accessToken);
        var storeB = await _f.CreateStoreAsync(admin.accessToken);
        var empB = await _f.CreateEmployeeAsync(admin.accessToken, storeB);

        var resp = await _f.Authed(empB.accessToken).GetAsync($"/api/stores/{storeA}/readiness");
        resp.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}
