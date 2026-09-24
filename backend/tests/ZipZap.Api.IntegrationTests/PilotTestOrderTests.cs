using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using ZipZap.Modules.Ordering.Infrastructure;

namespace ZipZap.Api.IntegrationTests;

/// <summary>
/// Pilotaż W1: zamówienia testowe bez opłaty, wyłącznie dla zaproszonych testerów (rola Tester),
/// z dziennym limitem na testera.
/// </summary>
[Collection("w1")]
public sealed class PilotTestOrderTests
{
    private readonly TestModeApiFactory _f;
    public PilotTestOrderTests(TestModeApiFactory f) => _f = f;

    private sealed record OrderDto(Guid id, string status, string paymentMode);
    private sealed record SlotDto(Guid id, int remainingCapacity);
    private sealed record PublicConfig(string paymentMode);

    private async Task<int> RemainingAsync(OrderScenario.Setup s)
        => (await _f.Anon().GetFromJsonAsync<List<SlotDto>>($"/api/ordering/stores/{s.StoreId}/slots"))!
            .Single(x => x.id == s.SlotId).remainingCapacity;

    [Fact]
    public async Task Public_config_announces_test_payment_mode()
        => (await _f.Anon().GetFromJsonAsync<PublicConfig>("/api/config/public"))!.paymentMode.Should().Be("test");

    [Fact]
    public async Task Regular_customer_is_refused_and_no_slot_capacity_is_consumed()
    {
        var s = await OrderScenario.StoreWithSlotAsync(_f);
        var before = await RemainingAsync(s);
        var customer = await _f.RegisterCustomerAsync(); // zwykłe konto — bez roli Tester

        var resp = await OrderScenario.CheckoutAsync(_f, s, customer.accessToken);

        resp.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await resp.Content.ReadAsStringAsync()).Should().Contain("tylko dla zaproszonych testerów");
        (await RemainingAsync(s)).Should().Be(before);
    }

    [Fact]
    public async Task Tester_places_free_test_order_and_event_carries_test_mode()
    {
        var s = await OrderScenario.StoreWithSlotAsync(_f);
        var tester = await OrderScenario.CustomerWithRoleAsync(_f, s.AdminToken, "Tester");

        var resp = await OrderScenario.CheckoutAsync(_f, s, tester.accessToken);
        resp.EnsureSuccessStatusCode();
        var order = (await resp.Content.ReadFromJsonAsync<OrderDto>())!;
        order.paymentMode.Should().Be("test");
        order.status.Should().Be("Placed");

        // Zdarzenie OrderPlaced niesie tryb „test" → moduł Payments nie tworzy płatności (test jednostkowy handlera).
        using var scope = _f.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<OrderingDbContext>();
        var key = order.id.ToString();
        var payloads = await db.OutboxMessages.AsNoTracking()
            .Where(m => m.Payload.Contains(key)).Select(m => m.Payload).ToListAsync();
        payloads.Should().ContainSingle(p => p.Contains("\"PaymentMode\":\"test\"", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Tester_daily_cap_is_enforced()
    {
        var s = await OrderScenario.StoreWithSlotAsync(_f);
        var tester = await OrderScenario.CustomerWithRoleAsync(_f, s.AdminToken, "Tester");

        for (var i = 0; i < TestModeApiFactory.DailyCap; i++)
            (await OrderScenario.CheckoutAsync(_f, s, tester.accessToken)).EnsureSuccessStatusCode();

        var over = await OrderScenario.CheckoutAsync(_f, s, tester.accessToken);
        over.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await over.Content.ReadAsStringAsync()).Should().Contain("limit");
    }
}
