using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using ZipZap.Modules.Ordering.Infrastructure;

namespace ZipZap.Api.IntegrationTests;

/// <summary>Współbieżne checkouty: wielu klientów na jeden termin, powtórzenia tego samego żądania.</summary>
[Collection("api")]
public sealed class CheckoutConcurrencyTests
{
    private readonly ApiFactory _f;
    public CheckoutConcurrencyTests(ApiFactory f) => _f = f;

    private sealed record OrderDto(Guid id);

    private async Task<List<(AuthDto customer, OrderScenario.PreparedCart cart)>> CustomersWithCartsAsync(
        OrderScenario.Setup s, int count)
    {
        var list = new List<(AuthDto, OrderScenario.PreparedCart)>();
        for (var i = 0; i < count; i++)
        {
            var c = await _f.RegisterCustomerAsync();
            list.Add((c, await OrderScenario.PrepareCartAsync(_f, s, c.accessToken)));
        }
        return list;
    }

    private async Task<(int reserved, int orders)> SlotStateAsync(OrderScenario.Setup s)
    {
        using var scope = _f.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<OrderingDbContext>();
        var reserved = (await db.TimeSlots.AsNoTracking().SingleAsync(t => t.Id == s.SlotId)).ReservedCount;
        var orders = await db.Orders.CountAsync(o => o.TimeSlotId == s.SlotId);
        return (reserved, orders);
    }

    [Fact]
    public async Task Parallel_checkouts_of_many_customers_into_a_roomy_slot_all_succeed()
    {
        const int customers = 10;
        var s = await OrderScenario.StoreWithSlotAsync(_f, maxOrders: 30);
        var work = await CustomersWithCartsAsync(s, customers);

        var responses = await OrderScenario.FireTogetherAsync(work.Select(w => (Func<Task<HttpResponseMessage>>)(
            () => OrderScenario.CheckoutCartAsync(_f, s, w.customer.accessToken, w.cart))).ToList());

        var bodies = await Task.WhenAll(responses.Select(r => r.Content.ReadAsStringAsync()));
        responses.Should().OnlyContain(r => r.IsSuccessStatusCode,
            "slot ma wolne miejsca — konflikt współbieżny nie może kończyć się 409 ({0})", string.Join(" | ", bodies.Distinct()));
        (await SlotStateAsync(s)).Should().Be((customers, customers), "licznik rezerwacji = liczba zamówień");
    }

    [Fact]
    public async Task Parallel_checkouts_into_a_nearly_full_slot_fill_it_exactly_without_overbooking()
    {
        const int capacity = 3, customers = 8;
        var s = await OrderScenario.StoreWithSlotAsync(_f, maxOrders: capacity);
        var work = await CustomersWithCartsAsync(s, customers);

        var responses = await OrderScenario.FireTogetherAsync(work.Select(w => (Func<Task<HttpResponseMessage>>)(
            () => OrderScenario.CheckoutCartAsync(_f, s, w.customer.accessToken, w.cart))).ToList());

        responses.Count(r => r.IsSuccessStatusCode).Should().Be(capacity);
        responses.Where(r => !r.IsSuccessStatusCode).Should().OnlyContain(r => r.StatusCode == HttpStatusCode.Conflict);
        (await SlotStateAsync(s)).Should().Be((capacity, capacity));

        // Pełny slot: kolejny klient dostaje 409, nic się nie zmienia.
        var late = (await CustomersWithCartsAsync(s, 1))[0];
        var resp = await OrderScenario.CheckoutCartAsync(_f, s, late.customer.accessToken, late.cart);
        resp.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await resp.Content.ReadAsStringAsync()).Should().Contain("pełny");
        (await SlotStateAsync(s)).Should().Be((capacity, capacity));
    }

    [Fact]
    public async Task Parallel_checkouts_of_the_same_cart_without_idempotency_key_create_one_order()
    {
        var s = await OrderScenario.StoreWithSlotAsync(_f, maxOrders: 20);
        var (customer, cart) = (await CustomersWithCartsAsync(s, 1))[0];

        var responses = await OrderScenario.FireTogetherAsync(Enumerable.Range(0, 4).Select(_ => (Func<Task<HttpResponseMessage>>)(
            () => OrderScenario.CheckoutCartAsync(_f, s, customer.accessToken, cart))).ToList());

        responses.Count(r => r.IsSuccessStatusCode).Should().Be(1, "jeden koszyk = jedno zamówienie");
        responses.Where(r => !r.IsSuccessStatusCode).Should().OnlyContain(r =>
            r.StatusCode == HttpStatusCode.Conflict || r.StatusCode == HttpStatusCode.BadRequest);
        (await SlotStateAsync(s)).Should().Be((1, 1));
    }

    [Fact]
    public async Task Parallel_retries_with_the_same_idempotency_key_return_one_order()
    {
        const int attempts = 4;
        var s = await OrderScenario.StoreWithSlotAsync(_f);
        var (customer, cart) = (await CustomersWithCartsAsync(s, 1))[0];
        var key = $"it-{Guid.NewGuid():N}";

        var responses = await OrderScenario.FireTogetherAsync(Enumerable.Range(0, attempts).Select(_ => (Func<Task<HttpResponseMessage>>)(
            () => OrderScenario.CheckoutCartAsync(_f, s, customer.accessToken, cart, idempotencyKey: key))).ToList());

        responses.Should().OnlyContain(r => r.IsSuccessStatusCode, "powtórzenie z tym samym kluczem nie jest błędem");
        var ids = new HashSet<Guid>();
        foreach (var r in responses) ids.Add((await r.Content.ReadFromJsonAsync<OrderDto>())!.id);
        ids.Should().ContainSingle();
        (await SlotStateAsync(s)).Should().Be((1, 1));
    }
}
