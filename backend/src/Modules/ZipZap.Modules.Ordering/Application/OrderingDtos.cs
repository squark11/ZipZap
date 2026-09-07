using ZipZap.Modules.Ordering.Domain;

namespace ZipZap.Modules.Ordering.Application;

public sealed record CartItemDto(Guid ProductId, string ProductName, decimal UnitPrice, int Quantity, decimal LineTotal)
{
    public static CartItemDto From(CartItem i) => new(i.ProductId, i.ProductName, i.UnitPrice, i.Quantity, i.LineTotal);
}

public sealed record CartDto(Guid Id, Guid StoreId, string CartToken, string Status, decimal Subtotal, IReadOnlyList<CartItemDto> Items)
{
    public static CartDto From(Cart c) =>
        new(c.Id, c.StoreId, c.CartToken, c.Status.ToString(), c.Subtotal,
            c.Items.Select(CartItemDto.From).ToList());
}

public sealed record OrderItemDto(Guid ProductId, string ProductName, decimal UnitPrice, int Quantity, decimal LineTotal)
{
    public static OrderItemDto From(OrderItem i) => new(i.ProductId, i.ProductName, i.UnitPrice, i.Quantity, i.LineTotal);
}

public sealed record OrderStatusChangeDto(string? FromStatus, string ToStatus, DateTime ChangedAtUtc)
{
    public static OrderStatusChangeDto From(OrderStatusChange h) =>
        new(h.FromStatus?.ToString(), h.ToStatus.ToString(), h.ChangedAtUtc);
}

public sealed record OrderDto(
    Guid Id, Guid StoreId, Guid CustomerId, string Status,
    decimal Subtotal, decimal CommissionAmount, decimal DeliveryFee, decimal Total, string Currency,
    Guid DeliveryZoneId, Guid TimeSlotId, string DeliveryAddress, string ContactPhone, DateTime PlacedAtUtc,
    IReadOnlyList<OrderItemDto> Items, IReadOnlyList<OrderStatusChangeDto> History)
{
    public static OrderDto From(Order o) =>
        new(o.Id, o.StoreId, o.CustomerId, o.Status.ToString(),
            o.Subtotal, o.CommissionAmount, o.DeliveryFee, o.Total, o.Currency,
            o.DeliveryZoneId, o.TimeSlotId, o.DeliveryAddress, o.ContactPhone, o.PlacedAtUtc,
            o.Items.Select(OrderItemDto.From).ToList(),
            o.History.OrderBy(h => h.ChangedAtUtc).Select(OrderStatusChangeDto.From).ToList());
}

public sealed record DeliveryZoneDto(Guid Id, Guid StoreId, string Name, decimal DeliveryFee, bool IsActive)
{
    public static DeliveryZoneDto From(DeliveryZone z) => new(z.Id, z.StoreId, z.Name, z.DeliveryFee, z.IsActive);
}

public sealed record TimeSlotDto(
    Guid Id, Guid StoreId, Guid DeliveryZoneId, DateOnly Date,
    TimeOnly StartTime, TimeOnly EndTime, int MaxOrders, int ReservedCount, int RemainingCapacity)
{
    public static TimeSlotDto From(TimeSlot s) =>
        new(s.Id, s.StoreId, s.DeliveryZoneId, s.Date, s.StartTime, s.EndTime,
            s.MaxOrders, s.ReservedCount, s.RemainingCapacity);
}
