using ZipZap.BuildingBlocks.Domain;

namespace ZipZap.Modules.Ordering.Domain;

public enum CartStatus { Active, CheckedOut, Abandoned }

/// <summary>Pozycja koszyka (snapshot nazwy/ceny z chwili dodania).</summary>
public sealed class CartItem : Entity
{
    public Guid CartId { get; private set; }
    public Guid ProductId { get; private set; }
    public string ProductName { get; private set; } = default!;
    public decimal UnitPrice { get; private set; }
    public int Quantity { get; private set; }
    public decimal LineTotal => UnitPrice * Quantity;

    private CartItem() { } // EF

    public CartItem(Guid productId, string productName, decimal unitPrice, int quantity)
    {
        ProductId = productId;
        ProductName = productName;
        UnitPrice = unitPrice;
        Quantity = quantity;
    }

    internal void SetQuantity(int quantity) => Quantity = quantity;
    internal void Add(int quantity) => Quantity += quantity;
}

/// <summary>Koszyk klienta (gość przez token lub zalogowany), jeden sklep na koszyk.</summary>
public sealed class Cart : AggregateRoot
{
    private readonly List<CartItem> _items = new();

    public Guid StoreId { get; private set; }
    public Guid? CustomerId { get; private set; }
    public string CartToken { get; private set; } = default!;
    public CartStatus Status { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime UpdatedAtUtc { get; private set; }

    public IReadOnlyCollection<CartItem> Items => _items.AsReadOnly();
    public decimal Subtotal => _items.Sum(i => i.LineTotal);

    private Cart() { } // EF

    public static Cart Create(Guid storeId, Guid? customerId)
        => new()
        {
            Id = Guid.NewGuid(),
            StoreId = storeId,
            CustomerId = customerId,
            CartToken = Guid.NewGuid().ToString("n"),
            Status = CartStatus.Active,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow,
        };

    public void AddItem(Guid productId, string productName, decimal unitPrice, int quantity)
    {
        EnsureActive();
        if (quantity <= 0) throw new OrderingDomainException("Ilość musi być dodatnia.");

        var existing = _items.FirstOrDefault(i => i.ProductId == productId);
        if (existing is null) _items.Add(new CartItem(productId, productName, unitPrice, quantity));
        else existing.Add(quantity);
        Touch();
    }

    public void SetItemQuantity(Guid productId, int quantity)
    {
        EnsureActive();
        var item = _items.FirstOrDefault(i => i.ProductId == productId)
            ?? throw new OrderingDomainException("Pozycji nie ma w koszyku.");
        if (quantity <= 0) _items.Remove(item);
        else item.SetQuantity(quantity);
        Touch();
    }

    public void RemoveItem(Guid productId)
    {
        EnsureActive();
        _items.RemoveAll(i => i.ProductId == productId);
        Touch();
    }

    public void AssignCustomer(Guid customerId)
    {
        CustomerId = customerId;
        Touch();
    }

    public void MarkCheckedOut()
    {
        EnsureActive();
        Status = CartStatus.CheckedOut;
        Touch();
    }

    private void EnsureActive()
    {
        if (Status != CartStatus.Active)
            throw new OrderingDomainException("Koszyk nie jest już aktywny.");
    }

    private void Touch() => UpdatedAtUtc = DateTime.UtcNow;
}
