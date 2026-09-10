using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using ZipZap.Modules.Payments.Domain;
using ZipZap.Modules.Payments.Infrastructure;

namespace ZipZap.Api.IntegrationTests;

[Collection("api")]
public sealed class StoreBillingInvoiceTests
{
    private readonly ApiFactory _f;
    public StoreBillingInvoiceTests(ApiFactory f) => _f = f;

    private sealed record Invoice(string period, string plan, string basis, decimal? unitFee,
        int orderCount, decimal total, string currency);

    private async Task SeedLedgerAsync(Guid storeId, params decimal[] amounts)
    {
        using var scope = _f.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PaymentsDbContext>();
        foreach (var a in amounts)
            db.CommissionLedger.Add(new CommissionLedgerEntry(storeId, Guid.NewGuid(), a));
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task Plan_A_invoice_is_deliveries_times_flat_fee()
    {
        var admin = await _f.LoginAdminAsync();
        var storeId = Guid.Parse(await _f.CreateStoreAsync(admin.accessToken));
        var c = _f.Authed(admin.accessToken);

        await c.PutAsJsonAsync($"/api/payments/stores/{storeId}/billing", new { plan = "A" });
        await SeedLedgerAsync(storeId, 3.00m, 7.00m, 11.00m); // 3 dostawy

        var inv = await c.GetFromJsonAsync<Invoice>($"/api/payments/stores/{storeId}/invoice");
        inv!.plan.Should().Be("A");
        inv.basis.Should().Be("delivery");
        inv.orderCount.Should().Be(3);
        inv.unitFee.Should().Be(25m);
        inv.total.Should().Be(75m); // 3 × 25 zł (kwoty prowizji ignorowane w planie A)
    }

    [Fact]
    public async Task Plan_B_invoice_sums_commission()
    {
        var admin = await _f.LoginAdminAsync();
        var storeId = Guid.Parse(await _f.CreateStoreAsync(admin.accessToken));
        var c = _f.Authed(admin.accessToken);

        await c.PutAsJsonAsync($"/api/payments/stores/{storeId}/billing", new { plan = "B" });
        await SeedLedgerAsync(storeId, 5.00m, 7.50m);

        var inv = await c.GetFromJsonAsync<Invoice>($"/api/payments/stores/{storeId}/invoice");
        inv!.plan.Should().Be("B");
        inv.basis.Should().Be("commission");
        inv.orderCount.Should().Be(2);
        inv.total.Should().Be(12.50m);
    }
}
