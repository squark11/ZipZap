using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using ZipZap.Modules.Ordering.Infrastructure;

namespace ZipZap.Api.IntegrationTests;

/// <summary>Równoległe powtórzenia tego samego checkoutu (np. podwójne kliknięcie, ponowienie po timeoucie).</summary>
[Collection("api")]
public sealed class CheckoutConcurrencyTests
{
    private readonly ApiFactory _f;
    public CheckoutConcurrencyTests(ApiFactory f) => _f = f;

    private sealed record OrderDto(Guid id);

    [Fact]
    public async Task Parallel_retries_with_the_same_idempotency_key_return_one_order()
    {
        const int attempts = 4;
        var s = await OrderScenario.StoreWithSlotAsync(_f);
        var customer = await _f.RegisterCustomerAsync();
        var cart = await OrderScenario.PrepareCartAsync(_f, s, customer.accessToken);
        var key = $"it-{Guid.NewGuid():N}";

        using var go = new SemaphoreSlim(0);
        var tasks = Enumerable.Range(0, attempts).Select(async _ =>
        {
            await go.WaitAsync();
            return await OrderScenario.CheckoutCartAsync(_f, s, customer.accessToken, cart, idempotencyKey: key);
        }).ToList();
        go.Release(attempts);
        var responses = await Task.WhenAll(tasks);

        responses.Should().OnlyContain(r => r.IsSuccessStatusCode, "powtórzenie z tym samym kluczem nie jest błędem");
        var ids = new HashSet<Guid>();
        foreach (var r in responses) ids.Add((await r.Content.ReadFromJsonAsync<OrderDto>())!.id);
        ids.Should().ContainSingle();

        using var scope = _f.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<OrderingDbContext>();
        var customerId = Guid.Parse(customer.user.id);
        (await db.Orders.CountAsync(o => o.CustomerId == customerId)).Should().Be(1);
        (await db.TimeSlots.SingleAsync(t => t.Id == s.SlotId)).ReservedCount.Should().Be(1);
    }
}
