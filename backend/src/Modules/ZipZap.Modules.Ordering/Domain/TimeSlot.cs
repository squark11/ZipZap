using ZipZap.BuildingBlocks.Domain;

namespace ZipZap.Modules.Ordering.Domain;

/// <summary>
/// Okno czasowe dostawy z LIMITEM zamówień na slot. Rezerwacja atomowa
/// (współbieżność zabezpieczona tokenem xmin w warstwie infrastruktury).
/// </summary>
public sealed class TimeSlot : AggregateRoot
{
    public Guid StoreId { get; private set; }
    public Guid DeliveryZoneId { get; private set; }
    public DateOnly Date { get; private set; }
    public TimeOnly StartTime { get; private set; }
    public TimeOnly EndTime { get; private set; }
    public int MaxOrders { get; private set; }
    public int ReservedCount { get; private set; }

    public bool HasCapacity => ReservedCount < MaxOrders;
    public int RemainingCapacity => Math.Max(0, MaxOrders - ReservedCount);

    private TimeSlot() { } // EF

    public TimeSlot(Guid storeId, Guid deliveryZoneId, DateOnly date,
        TimeOnly startTime, TimeOnly endTime, int maxOrders)
    {
        if (maxOrders <= 0) throw new OrderingDomainException("Limit zamówień na slot musi być dodatni.");
        if (endTime <= startTime) throw new OrderingDomainException("Koniec okna musi być po jego początku.");
        StoreId = storeId;
        DeliveryZoneId = deliveryZoneId;
        Date = date;
        StartTime = startTime;
        EndTime = endTime;
        MaxOrders = maxOrders;
        ReservedCount = 0;
    }

    /// <summary>Rezerwuje miejsce w slocie; rzuca, gdy slot jest pełny.</summary>
    public void Reserve()
    {
        if (!HasCapacity)
            throw new OrderingDomainException("Wybrany slot dostawy jest już pełny.");
        ReservedCount++;
    }

    public void Release()
    {
        if (ReservedCount > 0) ReservedCount--;
    }
}
