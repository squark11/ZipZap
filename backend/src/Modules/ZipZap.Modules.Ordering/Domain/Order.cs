using ZipZap.BuildingBlocks.Domain;

namespace ZipZap.Modules.Ordering.Domain;

/// <summary>Zamówienie (korzeń agregatu) ze snapshotami pozycji i maszyną stanów.</summary>
public sealed class Order : AggregateRoot
{
    // Dozwolone przejścia stanów.
    private static readonly IReadOnlyDictionary<OrderStatus, OrderStatus[]> Transitions =
        new Dictionary<OrderStatus, OrderStatus[]>
        {
            [OrderStatus.Placed] = new[] { OrderStatus.Confirmed, OrderStatus.Cancelled },
            [OrderStatus.Confirmed] = new[] { OrderStatus.Picking, OrderStatus.Cancelled },
            [OrderStatus.Picking] = new[] { OrderStatus.ReadyForPickup, OrderStatus.Cancelled },
            [OrderStatus.ReadyForPickup] = new[] { OrderStatus.InDelivery, OrderStatus.Cancelled },
            [OrderStatus.InDelivery] = new[] { OrderStatus.Delivered },
            [OrderStatus.Delivered] = new[] { OrderStatus.Completed },
            [OrderStatus.Completed] = Array.Empty<OrderStatus>(),
            [OrderStatus.Cancelled] = Array.Empty<OrderStatus>(),
        };

    private readonly List<OrderItem> _items = new();
    private readonly List<OrderStatusChange> _history = new();

    public Guid StoreId { get; private set; }
    public Guid CustomerId { get; private set; }
    public OrderStatus Status { get; private set; }

    public decimal Subtotal { get; private set; }
    public decimal CommissionAmount { get; private set; }
    public decimal DeliveryFee { get; private set; }
    public decimal Total { get; private set; }
    public string Currency { get; private set; } = "PLN";

    public Guid DeliveryZoneId { get; private set; }
    public Guid TimeSlotId { get; private set; }
    public string DeliveryAddress { get; private set; } = default!;
    public string ContactPhone { get; private set; } = default!;
    public string? IdempotencyKey { get; private set; }
    public DateTime PlacedAtUtc { get; private set; }

    public IReadOnlyCollection<OrderItem> Items => _items.AsReadOnly();
    public IReadOnlyCollection<OrderStatusChange> History => _history.AsReadOnly();

    private Order() { } // EF

    public static Order Place(
        Guid storeId,
        Guid customerId,
        IReadOnlyCollection<OrderLine> lines,
        decimal commissionRate,
        decimal deliveryFee,
        Guid deliveryZoneId,
        Guid timeSlotId,
        string deliveryAddress,
        string contactPhone,
        string currency = "PLN")
    {
        if (lines is null || lines.Count == 0)
            throw new OrderingDomainException("Nie można złożyć zamówienia z pustego koszyka.");
        if (commissionRate is < 0 or > 1)
            throw new OrderingDomainException("Prowizja musi być w zakresie 0–1.");
        if (deliveryFee < 0)
            throw new OrderingDomainException("Opłata za dostawę nie może być ujemna.");

        var order = new Order
        {
            Id = Guid.NewGuid(),
            StoreId = storeId,
            CustomerId = customerId,
            Status = OrderStatus.Placed,
            DeliveryFee = deliveryFee,
            DeliveryZoneId = deliveryZoneId,
            TimeSlotId = timeSlotId,
            DeliveryAddress = deliveryAddress,
            ContactPhone = contactPhone,
            Currency = currency,
            PlacedAtUtc = DateTime.UtcNow,
        };

        foreach (var line in lines)
            order._items.Add(new OrderItem(line.ProductId, line.ProductName, line.UnitPrice, line.Quantity));

        order.Subtotal = order._items.Sum(i => i.LineTotal);
        order.CommissionAmount = Math.Round(order.Subtotal * commissionRate, 2, MidpointRounding.AwayFromZero);
        order.Total = order.Subtotal + deliveryFee;

        order._history.Add(new OrderStatusChange(null, OrderStatus.Placed, customerId));
        order.Raise(new OrderPlacedDomainEvent(order.Id, storeId, order.Total));
        return order;
    }

    public void Confirm(Guid? by = null) => ChangeStatus(OrderStatus.Confirmed, by);
    public void StartPicking(Guid? by = null) => ChangeStatus(OrderStatus.Picking, by);
    public void MarkReadyForPickup(Guid? by = null) => ChangeStatus(OrderStatus.ReadyForPickup, by);
    public void PickUp(Guid? by = null) => ChangeStatus(OrderStatus.InDelivery, by);
    public void MarkDelivered(Guid? by = null) => ChangeStatus(OrderStatus.Delivered, by);
    public void Complete(Guid? by = null) => ChangeStatus(OrderStatus.Completed, by);
    public void Cancel(Guid? by = null) => ChangeStatus(OrderStatus.Cancelled, by);

    public void SetIdempotencyKey(string? key) => IdempotencyKey = key;

    public bool CanTransitionTo(OrderStatus target) => Transitions[Status].Contains(target);

    private void ChangeStatus(OrderStatus target, Guid? by)
    {
        if (!Transitions[Status].Contains(target))
            throw new OrderingDomainException($"Niedozwolone przejście: {Status} → {target}.");

        _history.Add(new OrderStatusChange(Status, target, by));
        Status = target;
    }
}
