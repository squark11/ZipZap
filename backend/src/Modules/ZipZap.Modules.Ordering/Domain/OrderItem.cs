using ZipZap.BuildingBlocks.Domain;

namespace ZipZap.Modules.Ordering.Domain;

/// <summary>Pozycja zamówienia — SNAPSHOT nazwy i ceny z momentu złożenia.</summary>
public sealed class OrderItem : Entity
{
    public Guid OrderId { get; private set; }
    public Guid ProductId { get; private set; }
    public string ProductName { get; private set; } = default!;
    public decimal UnitPrice { get; private set; }
    /// <summary>Jednostka pozycji (kg/szt/...) — snapshot z chwili złożenia.</summary>
    public string Unit { get; private set; } = "szt";
    public int Quantity { get; private set; }
    public decimal LineTotal => UnitPrice * Quantity;

    private OrderItem() { } // EF

    public OrderItem(Guid productId, string productName, decimal unitPrice, string unit, int quantity)
    {
        if (quantity <= 0) throw new OrderingDomainException("Ilość pozycji musi być dodatnia.");
        if (unitPrice < 0) throw new OrderingDomainException("Cena pozycji nie może być ujemna.");
        ProductId = productId;
        ProductName = productName;
        UnitPrice = unitPrice;
        Unit = unit;
        Quantity = quantity;
    }
}
