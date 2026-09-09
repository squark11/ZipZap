using System.Net;
using FluentAssertions;
using Xunit;

namespace ZipZap.Api.IntegrationTests;

[Collection("api")]
public sealed class TenantIsolationTests
{
    private readonly ApiFactory _f;
    public TenantIsolationTests(ApiFactory f) => _f = f;

    [Fact]
    public async Task Employee_can_read_own_store_orders_but_not_another_store()
    {
        var admin = await _f.LoginAdminAsync();
        var storeA = await _f.CreateStoreAsync(admin.accessToken);
        var storeB = await _f.CreateStoreAsync(admin.accessToken);
        var empA = await _f.CreateEmployeeAsync(admin.accessToken, storeA);

        empA.user.storeIds.Should().Contain(storeA);
        var client = _f.Authed(empA.accessToken);

        var own = await client.GetAsync($"/api/ordering/stores/{storeA}/orders");
        own.StatusCode.Should().Be(HttpStatusCode.OK); // własny sklep

        var other = await client.GetAsync($"/api/ordering/stores/{storeB}/orders");
        other.StatusCode.Should().Be(HttpStatusCode.Forbidden); // cudzy sklep — izolacja
    }

    [Fact]
    public async Task Customer_cannot_read_store_orders()
    {
        var admin = await _f.LoginAdminAsync();
        var store = await _f.CreateStoreAsync(admin.accessToken);
        var customer = await _f.RegisterCustomerAsync();

        var resp = await _f.Authed(customer.accessToken).GetAsync($"/api/ordering/stores/{store}/orders");
        resp.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}
