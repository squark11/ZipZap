using ZipZap.BuildingBlocks.Domain;

namespace ZipZap.Modules.Ordering.Domain;

/// <summary>Strefa dostaw sklepu z opłatą za dostawę i (opcjonalnie) kodami pocztowymi.</summary>
public sealed class DeliveryZone : Entity
{
    public Guid StoreId { get; private set; }
    public string Name { get; private set; } = default!;
    public decimal DeliveryFee { get; private set; }
    public bool IsActive { get; private set; }

    /// <summary>Kody pocztowe (znormalizowane, bez myślnika). Pusto = strefa obsługuje wszędzie.</summary>
    public List<string> PostalCodes { get; private set; } = new();

    private DeliveryZone() { } // EF

    public DeliveryZone(Guid storeId, string name, decimal deliveryFee, IEnumerable<string>? postalCodes = null)
    {
        if (deliveryFee < 0) throw new OrderingDomainException("Opłata za dostawę nie może być ujemna.");
        StoreId = storeId;
        Name = name.Trim();
        DeliveryFee = deliveryFee;
        IsActive = true;
        if (postalCodes is not null)
            PostalCodes = postalCodes.Select(Normalize).Where(c => c.Length > 0).Distinct().ToList();
    }

    public bool Covers(string postalCode) => PostalCodes.Count == 0 || PostalCodes.Contains(Normalize(postalCode));

    private static string Normalize(string code) => code.Trim().Replace("-", "").ToUpperInvariant();
}
