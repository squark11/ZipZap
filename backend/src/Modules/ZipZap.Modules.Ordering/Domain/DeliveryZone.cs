using ZipZap.BuildingBlocks.Domain;

namespace ZipZap.Modules.Ordering.Domain;

/// <summary>Strefa dostaw sklepu z opłatą za dostawę i (opcjonalnie) kodami pocztowymi.</summary>
public sealed class DeliveryZone : Entity
{
    private readonly List<string> _postalCodes = new();

    public Guid StoreId { get; private set; }
    public string Name { get; private set; } = default!;
    public decimal DeliveryFee { get; private set; }
    public bool IsActive { get; private set; }

    public IReadOnlyCollection<string> PostalCodes => _postalCodes.AsReadOnly();

    private DeliveryZone() { } // EF

    public DeliveryZone(Guid storeId, string name, decimal deliveryFee, IEnumerable<string>? postalCodes = null)
    {
        if (deliveryFee < 0) throw new OrderingDomainException("Opłata za dostawę nie może być ujemna.");
        StoreId = storeId;
        Name = name.Trim();
        DeliveryFee = deliveryFee;
        IsActive = true;
        if (postalCodes is not null)
            _postalCodes.AddRange(postalCodes.Select(Normalize));
    }

    public bool Covers(string postalCode) => _postalCodes.Count == 0 || _postalCodes.Contains(Normalize(postalCode));

    private static string Normalize(string code) => code.Trim().Replace("-", "").ToUpperInvariant();
}
