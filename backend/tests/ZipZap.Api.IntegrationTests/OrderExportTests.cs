using System.Net;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using ZipZap.Modules.Ordering.Domain;
using ZipZap.Modules.Ordering.Infrastructure;

namespace ZipZap.Api.IntegrationTests;

[Collection("api")]
public sealed class OrderExportTests
{
    private readonly ApiFactory _f;
    public OrderExportTests(ApiFactory f) => _f = f;

    /// <summary>Seeduje zamówienie bezpośrednio (2 pozycje, subtotal 20 zł, prowizja 10%, dostawa 5 zł).</summary>
    private async Task SeedOrderAsync(Guid storeId)
    {
        using var scope = _f.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<OrderingDbContext>();
        var lines = new[]
        {
            new OrderLine(Guid.NewGuid(), "Produkt A", 10.00m, "szt", 1),
            new OrderLine(Guid.NewGuid(), "Produkt B", 5.00m, "szt", 2),
        };
        var order = Order.Place(storeId, Guid.NewGuid(), lines, commissionRate: 0.10m, deliveryFee: 5.00m,
            Guid.NewGuid(), Guid.NewGuid(), "ul. Testowa 1", "600100200");
        db.Orders.Add(order);
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task Export_returns_header_and_order_row_with_accounting_totals()
    {
        var admin = await _f.LoginAdminAsync();
        var storeId = Guid.Parse(await _f.CreateStoreAsync(admin.accessToken));
        await SeedOrderAsync(storeId);

        var c = _f.Authed(admin.accessToken);
        var resp = await c.GetAsync($"/api/ordering/stores/{storeId}/orders/export");
        resp.EnsureSuccessStatusCode();
        resp.Content.Headers.ContentType!.MediaType.Should().Be("text/csv");
        var csv = await resp.Content.ReadAsStringAsync();

        csv.Should().Contain("numer;data;status;pozycje;produkty;prowizja;dostawa;suma;waluta");
        // subtotal 20,00 · prowizja 2,00 · dostawa 5,00 · suma 25,00 · 2 pozycje
        csv.Should().Contain(";Złożone;2;20,00;2,00;5,00;25,00;PLN");
    }

    [Fact]
    public async Task Export_empty_store_returns_only_header()
    {
        var admin = await _f.LoginAdminAsync();
        var storeId = Guid.Parse(await _f.CreateStoreAsync(admin.accessToken));
        var c = _f.Authed(admin.accessToken);

        var csv = await c.GetStringAsync($"/api/ordering/stores/{storeId}/orders/export");
        csv.Trim().Should().EndWith("waluta");
        csv.Should().NotContain(";Złożone;");
    }

    [Fact]
    public async Task Other_store_employee_cannot_export_orders()
    {
        var admin = await _f.LoginAdminAsync();
        var storeA = Guid.Parse(await _f.CreateStoreAsync(admin.accessToken));
        var storeB = Guid.Parse(await _f.CreateStoreAsync(admin.accessToken));
        var empB = await _f.CreateEmployeeAsync(admin.accessToken, storeB.ToString());

        var resp = await _f.Authed(empB.accessToken).GetAsync($"/api/ordering/stores/{storeA}/orders/export");
        resp.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}
