using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Xunit;
using ZipZap.Contracts.Ordering;
using ZipZap.Modules.Payments.Application;
using ZipZap.Modules.Payments.Domain;
using ZipZap.Modules.Payments.Infrastructure;

namespace ZipZap.Modules.Payments.Tests;

/// <summary>
/// Pilotaż bez płatności: gdy nie ma zarejestrowanego dostawcy (tryb publiczny wyłącza mocka),
/// zamówienie jest testowe i NIEKSIĘGOWE — brak sesji, brak autoryzacji, brak prowizji.
/// </summary>
public class PaymentsEventHandlersTests
{
    private static PaymentsDbContext NewDb() => new(new DbContextOptionsBuilder<PaymentsDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private static PaymentProviderRegistry NoProviders() => new(Array.Empty<IPaymentProvider>(), "mock");

    private static PaymentProviderRegistry WithMock() =>
        new(new IPaymentProvider[] { new MockPaymentProvider(Options.Create(new PaymentsOptions())) }, "mock");

    private static OrderPlaced Placed(Guid orderId) =>
        new(orderId, Guid.NewGuid(), Guid.NewGuid(), 12m, 1.2m, 8m, 20m, Guid.NewGuid());

    [Fact]
    public async Task Without_provider_order_is_test_only_no_session_no_authorization()
    {
        using var db = NewDb();
        var handlers = new PaymentsEventHandlers(db, NoProviders(), new NullStorePaymentGateway());
        var orderId = Guid.NewGuid();

        await handlers.HandleAsync(Placed(orderId));

        var p = await db.Payments.SingleAsync();
        p.OrderId.Should().Be(orderId);
        p.Status.Should().Be(PaymentStatus.Pending);
        p.SessionId.Should().BeNull();
        p.Provider.Should().BeNull();
        p.AuthorizedAtUtc.Should().BeNull();
    }

    [Fact]
    public async Task Delivered_test_order_books_no_commission_and_is_not_settled()
    {
        using var db = NewDb();
        var handlers = new PaymentsEventHandlers(db, NoProviders(), new NullStorePaymentGateway());
        var placed = Placed(Guid.NewGuid());
        await handlers.HandleAsync(placed);

        await handlers.HandleAsync(new OrderDelivered(placed.OrderId, placed.StoreId));

        (await db.CommissionLedger.CountAsync()).Should().Be(0);
        (await db.Payments.SingleAsync()).Status.Should().Be(PaymentStatus.Pending);
    }

    [Fact]
    public async Task Delivered_unpaid_order_books_no_commission_even_with_provider()
    {
        using var db = NewDb();
        var handlers = new PaymentsEventHandlers(db, WithMock(), new NullStorePaymentGateway());
        var placed = Placed(Guid.NewGuid());
        await handlers.HandleAsync(placed); // sesja utworzona, ale brak webhooka → Pending

        await handlers.HandleAsync(new OrderDelivered(placed.OrderId, placed.StoreId));

        (await db.CommissionLedger.CountAsync()).Should().Be(0);
        (await db.Payments.SingleAsync()).Status.Should().Be(PaymentStatus.Pending);
    }

    [Fact]
    public async Task Delivered_authorized_order_books_commission_and_settles()
    {
        using var db = NewDb();
        var handlers = new PaymentsEventHandlers(db, WithMock(), new NullStorePaymentGateway());
        var placed = Placed(Guid.NewGuid());
        await handlers.HandleAsync(placed);
        var payment = await db.Payments.SingleAsync();
        payment.Authorize("ref-1").Should().BeTrue(); // odpowiednik zweryfikowanego webhooka
        await db.SaveChangesAsync();

        await handlers.HandleAsync(new OrderDelivered(placed.OrderId, placed.StoreId));

        (await db.CommissionLedger.SingleAsync()).Amount.Should().Be(1.2m);
        (await db.Payments.SingleAsync()).Status.Should().Be(PaymentStatus.Settled);
    }
}
