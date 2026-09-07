using ZipZap.BuildingBlocks.Domain;

namespace ZipZap.Modules.Ordering.Domain;

/// <summary>Wartość wejściowa do złożenia zamówienia (snapshot pozycji koszyka).</summary>
public readonly record struct OrderLine(Guid ProductId, string ProductName, decimal UnitPrice, int Quantity);

/// <summary>Zdarzenie domenowe: zamówienie złożone (wewnątrz modułu).</summary>
public sealed record OrderPlacedDomainEvent(Guid OrderId, Guid StoreId, decimal Total) : IDomainEvent
{
    public Guid Id { get; } = Guid.NewGuid();
    public DateTime OccurredAtUtc { get; } = DateTime.UtcNow;
}
