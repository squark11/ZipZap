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

    [Fact]
    public async Task Delivered_W1_test_order_creates_no_payment_and_no_commission()
    {
        var s = await OrderScenario.StoreWithSlotAsync(_f);
        var tester = await OrderScenario.CustomerWithRoleAsync(_f, s.AdminToken, "Tester");
        var ready = await DeliveryScenario.ReadyOrderAsync(_f, s, tester);
        var driver = await _f.CreateStaffAsync(s.AdminToken, s.StoreId.ToString(), "Driver");
        var dc = _f.Authed(driver.accessToken);

        (await DeliveryScenario.AssignAsync(_f, s.AdminToken, s.StoreId, ready.DeliveryId, Guid.Parse(driver.user.id))).EnsureSuccessStatusCode();
        (await dc.PostAsync($"/api/delivery/{ready.DeliveryId}/pick-up", null)).EnsureSuccessStatusCode();
        (await dc.PostAsync($"/api/delivery/{ready.DeliveryId}/delivered", null)).EnsureSuccessStatusCode();

        string status = "";
        for (var i = 0; i < 40 && status != "Delivered"; i++)
        {
            await DeliveryScenario.FlushOutboxAsync(_f);
            status = (await _f.Authed(s.AdminToken).GetFromJsonAsync<OrderDto>($"/api/ordering/orders/{ready.OrderId}"))!.status;
            if (status != "Delivered") await Task.Delay(100);
        }
        status.Should().Be("Delivered", "OrderDelivered przeszło przez moduły");

        using var scope = _f.Services.CreateScope();
        var payments = scope.ServiceProvider.GetRequiredService<ZipZap.Modules.Payments.Infrastructure.PaymentsDbContext>();
        (await payments.Payments.CountAsync(p => p.OrderId == ready.OrderId)).Should().Be(0, "W1: bez płatności");
        (await payments.CommissionLedger.CountAsync(c => c.OrderId == ready.OrderId)).Should().Be(0, "W1: bez księgowania prowizji");
    }

    [Fact]
    public async Task Parallel_checkouts_of_one_tester_never_exceed_the_daily_cap()
    {
        const int attempts = 8;
        var s = await OrderScenario.StoreWithSlotAsync(_f);
        var tester = await OrderScenario.CustomerWithRoleAsync(_f, s.AdminToken, "Tester");
        // Każdy checkout na INNYM terminie dostawy — inaczej współbieżność slotu (xmin) sama serializuje
        // żądania i test nie sprawdzałby limitu testera.
        var date = DateOnly.FromDateTime(DateTime.UtcNow.Date.AddDays(3));
        var work = new List<(OrderScenario.PreparedCart cart, Guid slot)>();
        for (var i = 0; i < attempts; i++)
            work.Add((await OrderScenario.PrepareCartAsync(_f, s, tester.accessToken),
                await OrderScenario.AddSlotAsync(_f, s, date, $"{8 + i:00}:00:00", $"{9 + i:00}:00:00")));

        // Wszystkie checkouty (różne koszyki, bez klucza idempotencji) startują jednocześnie.
        using var go = new SemaphoreSlim(0);
        var tasks = work.Select(async w =>
        {
            await go.WaitAsync();
            return await OrderScenario.CheckoutCartAsync(_f, s, tester.accessToken, w.cart, w.slot);
        }).ToList();
        go.Release(attempts);
        var responses = await Task.WhenAll(tasks);

        responses.Count(r => r.IsSuccessStatusCode).Should().Be(TestModeApiFactory.DailyCap);
        responses.Where(r => !r.IsSuccessStatusCode).Should().OnlyContain(r => r.StatusCode == HttpStatusCode.Conflict);

        using var scope = _f.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<OrderingDbContext>();
        var testerId = Guid.Parse(tester.user.id);
        (await db.Orders.CountAsync(o => o.CustomerId == testerId)).Should().Be(TestModeApiFactory.DailyCap);
    }
}
