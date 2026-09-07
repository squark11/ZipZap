using FluentAssertions;
using Xunit;
using ZipZap.Modules.Ordering.Domain;

namespace ZipZap.Modules.Ordering.Tests;

public class TimeSlotTests
{
    private static TimeSlot NewSlot(int maxOrders) => new(
        Guid.NewGuid(), Guid.NewGuid(),
        DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1)),
        new TimeOnly(10, 0), new TimeOnly(12, 0), maxOrders);

    [Fact]
    public void Reserve_under_capacity_increments()
    {
        var slot = NewSlot(maxOrders: 2);

        slot.Reserve();
        slot.ReservedCount.Should().Be(1);
        slot.HasCapacity.Should().BeTrue();

        slot.Reserve();
        slot.ReservedCount.Should().Be(2);
        slot.HasCapacity.Should().BeFalse();
        slot.RemainingCapacity.Should().Be(0);
    }

    [Fact]
    public void Reserve_at_capacity_throws()
    {
        var slot = NewSlot(maxOrders: 1);
        slot.Reserve();

        var act = () => slot.Reserve();
        act.Should().Throw<OrderingDomainException>();
        slot.ReservedCount.Should().Be(1); // bez zmiany
    }

    [Fact]
    public void Release_decrements_but_not_below_zero()
    {
        var slot = NewSlot(maxOrders: 2);
        slot.Reserve();

        slot.Release();
        slot.ReservedCount.Should().Be(0);

        slot.Release(); // nie schodzi poniżej zera
        slot.ReservedCount.Should().Be(0);
    }

    [Fact]
    public void Slot_with_non_positive_capacity_is_rejected()
    {
        var act = () => NewSlot(maxOrders: 0);
        act.Should().Throw<OrderingDomainException>();
    }
}
