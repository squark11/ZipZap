using FluentAssertions;
using Xunit;
using ZipZap.Modules.Ordering.Domain;

namespace ZipZap.Modules.Ordering.Tests;

public class OrderTests
{
    private static readonly Guid StoreId = Guid.NewGuid();
    private static readonly Guid CustomerId = Guid.NewGuid();
    private static readonly Guid ZoneId = Guid.NewGuid();
    private static readonly Guid SlotId = Guid.NewGuid();

    private static Order PlaceSampleOrder(decimal commissionRate = 0.10m, decimal deliveryFee = 7.50m)
    {
        var lines = new List<OrderLine>
        {
            new(Guid.NewGuid(), "Marchew 1kg", 4.99m, 2), // 9.98
            new(Guid.NewGuid(), "Ziemniaki 1kg", 3.00m, 1), // 3.00
        };
        return Order.Place(StoreId, CustomerId, lines, commissionRate, deliveryFee,
            ZoneId, SlotId, "ul. Testowa 1, Koło", "600100200");
    }

    [Fact]
    public void Place_computes_totals_commission_and_raises_event()
    {
        var order = PlaceSampleOrder();

        order.Status.Should().Be(OrderStatus.Placed);
        order.Subtotal.Should().Be(12.98m);
        order.CommissionAmount.Should().Be(1.30m);           // round(12.98 * 0.10) = 1.30
        order.DeliveryFee.Should().Be(7.50m);
        order.Total.Should().Be(20.48m);                     // subtotal + delivery fee
        order.Items.Should().HaveCount(2);
        order.History.Should().ContainSingle(h => h.ToStatus == OrderStatus.Placed && h.FromStatus == null);
        order.DomainEvents.Should().ContainSingle(e => e is OrderPlacedDomainEvent);
    }

    [Fact]
    public void Place_with_empty_cart_throws()
    {
        var act = () => Order.Place(StoreId, CustomerId, new List<OrderLine>(), 0.10m, 5m,
            ZoneId, SlotId, "adres", "600");
        act.Should().Throw<OrderingDomainException>();
    }

    [Fact]
    public void Valid_status_flow_records_history()
    {
        var order = PlaceSampleOrder();

        order.Confirm();
        order.StartPicking();
        order.MarkReadyForPickup();
        order.PickUp();
        order.MarkDelivered();
        order.Complete();

        order.Status.Should().Be(OrderStatus.Completed);
        // Placed + 6 przejść
        order.History.Should().HaveCount(7);
    }

    [Fact]
    public void Invalid_transition_throws()
    {
        var order = PlaceSampleOrder(); // Placed

        var act = () => order.MarkDelivered(); // Placed -> Delivered niedozwolone
        act.Should().Throw<OrderingDomainException>();
        order.Status.Should().Be(OrderStatus.Placed);
    }

    [Fact]
    public void Cancel_allowed_before_pickup_but_not_after()
    {
        var order = PlaceSampleOrder();
        order.Confirm();
        order.StartPicking();
        order.MarkReadyForPickup();
        order.PickUp(); // InDelivery

        var act = () => order.Cancel();
        act.Should().Throw<OrderingDomainException>();
        order.Status.Should().Be(OrderStatus.InDelivery);
    }

    [Fact]
    public void Cancel_from_placed_succeeds()
    {
        var order = PlaceSampleOrder();
        order.Cancel();
        order.Status.Should().Be(OrderStatus.Cancelled);
    }
}
