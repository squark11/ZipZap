using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Xunit;

namespace ZipZap.Api.IntegrationTests;

[Collection("api")]
public sealed class MultiLocationTests
{
    private readonly ApiFactory _f;
    public MultiLocationTests(ApiFactory f) => _f = f;

    private sealed record Store(Guid id, string name);

    private Task<HttpResponseMessage> AddCategory(HttpClient c, Guid storeId, string name)
        => c.PostAsJsonAsync($"/api/catalog/stores/{storeId}/categories", new { name, sortOrder = 0, parentId = (Guid?)null });

    [Fact]
    public async Task Merchant_adds_location_then_manages_it_after_token_refresh()
    {
        var admin = await _f.LoginAdminAsync();
        var storeA = await _f.CreateStoreAsync(admin.accessToken);
        var emp = await _f.CreateEmployeeAsync(admin.accessToken, storeA); // pracownik sklepu A
        var empClient = _f.Authed(emp.accessToken);

        // 1) Pracownik dodaje kolejną lokalizację (nowy sklep przypisany do siebie).
        var addResp = await empClient.PostAsJsonAsync("/api/merchant/stores",
            new { name = $"Rapacz Rynek {Guid.NewGuid():N}".Substring(0, 16), city = "Kraków", commissionRate = 0.10m, minimumOrderValue = 0m });
        addResp.EnsureSuccessStatusCode();
        var storeB = (await addResp.Content.ReadFromJsonAsync<Store>())!.id;

        // 2) STARY token nie zawiera jeszcze sklepu B → zarządzanie B zabronione.
        (await AddCategory(empClient, storeB, "Pieczywo")).StatusCode.Should().Be(HttpStatusCode.Forbidden);

        // 3) Odświeżenie tokenu → nowy sklep w claimach.
        var refreshResp = await _f.Anon().PostAsJsonAsync("/api/identity/refresh", new { refreshToken = emp.refreshToken });
        refreshResp.EnsureSuccessStatusCode();
        var refreshed = (await refreshResp.Content.ReadFromJsonAsync<AuthDto>())!;
        var empClient2 = _f.Authed(refreshed.accessToken);

        // 4) Teraz pracownik zarządza OBIEMA lokalizacjami.
        (await AddCategory(empClient2, storeB, "Pieczywo")).EnsureSuccessStatusCode();
        (await AddCategory(empClient2, Guid.Parse(storeA), "Nabiał")).EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task Employee_cannot_manage_a_store_they_are_not_assigned_to()
    {
        var admin = await _f.LoginAdminAsync();
        var storeA = await _f.CreateStoreAsync(admin.accessToken);
        var storeB = await _f.CreateStoreAsync(admin.accessToken);
        var empA = await _f.CreateEmployeeAsync(admin.accessToken, storeA);

        (await AddCategory(_f.Authed(empA.accessToken), Guid.Parse(storeB), "Cokolwiek"))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Customer_cannot_add_a_location()
    {
        var customer = await _f.RegisterCustomerAsync();
        var resp = await _f.Authed(customer.accessToken)
            .PostAsJsonAsync("/api/merchant/stores", new { name = "Obcy sklep", city = "X", commissionRate = 0.1m, minimumOrderValue = 0m });
        resp.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}
