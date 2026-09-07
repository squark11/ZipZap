namespace ZipZap.Modules.Ordering.Api;

public sealed record CreateCartRequest(Guid StoreId);

public sealed record AddCartItemRequest(Guid ProductId, int Quantity);

public sealed record SetCartItemQuantityRequest(int Quantity);

public sealed record CreateZoneRequest(string Name, decimal DeliveryFee, string[]? PostalCodes);

public sealed record CreateSlotRequest(
    Guid DeliveryZoneId, DateOnly Date, TimeOnly StartTime, TimeOnly EndTime, int MaxOrders);

public sealed record PlaceOrderRequest(
    string Token, Guid DeliveryZoneId, Guid TimeSlotId, string DeliveryAddress, string ContactPhone);
