using System.Linq;
using ZipZap.BuildingBlocks.Domain;

namespace ZipZap.Modules.Ordering.Domain;

/// <summary>Zapisany adres dostawy klienta (należy do CustomerId = użytkownik).</summary>
public sealed class CustomerAddress : Entity
{
    public Guid CustomerId { get; private set; }
    public string Label { get; private set; } = default!;      // np. „Dom", „Praca"
    public string Street { get; private set; } = default!;      // ulica
    public string BuildingNo { get; private set; } = default!; // nr domu
    public string? ApartmentNo { get; private set; }           // nr lokalu (opc.)
    public string PostalCode { get; private set; } = default!; // kod pocztowy (np. 62-500)
    public string City { get; private set; } = default!;
    public string? Notes { get; private set; }                 // uwagi dla kuriera (opc.)
    public double? Latitude { get; private set; }
    public double? Longitude { get; private set; }
    public bool IsDefault { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }

    private CustomerAddress() { } // EF

    public CustomerAddress(Guid customerId, string label, string street, string buildingNo,
        string? apartmentNo, string postalCode, string city, string? notes,
        double? latitude, double? longitude, bool isDefault)
    {
        CustomerId = customerId;
        Set(label, street, buildingNo, apartmentNo, postalCode, city, notes, latitude, longitude);
        IsDefault = isDefault;
        CreatedAtUtc = DateTime.UtcNow;
    }

    public void Update(string label, string street, string buildingNo, string? apartmentNo,
        string postalCode, string city, string? notes, double? latitude, double? longitude)
        => Set(label, street, buildingNo, apartmentNo, postalCode, city, notes, latitude, longitude);

    private void Set(string label, string street, string buildingNo, string? apartmentNo,
        string postalCode, string city, string? notes, double? latitude, double? longitude)
    {
        if (string.IsNullOrWhiteSpace(street)) throw new OrderingDomainException("Podaj ulicę.");
        if (string.IsNullOrWhiteSpace(buildingNo)) throw new OrderingDomainException("Podaj numer domu.");
        if (string.IsNullOrWhiteSpace(city)) throw new OrderingDomainException("Podaj miasto.");
        if (!IsValidPostalCode(postalCode)) throw new OrderingDomainException("Podaj poprawny kod pocztowy (XX-XXX).");

        Label = string.IsNullOrWhiteSpace(label) ? "Adres" : label.Trim();
        Street = street.Trim();
        BuildingNo = buildingNo.Trim();
        ApartmentNo = string.IsNullOrWhiteSpace(apartmentNo) ? null : apartmentNo.Trim();
        PostalCode = FormatPostalCode(postalCode);
        City = city.Trim();
        Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim();
        Latitude = latitude;
        Longitude = longitude;
    }

    public void MakeDefault() => IsDefault = true;
    public void ClearDefault() => IsDefault = false;

    /// <summary>Kod pocztowy do dopasowania stref (bez myślnika, wielkie litery).</summary>
    public string NormalizedPostalCode() => PostalCode.Replace("-", "").ToUpperInvariant();

    /// <summary>Polski kod pocztowy: 5 cyfr (myślnik i spacje ignorowane).</summary>
    public static bool IsValidPostalCode(string? code)
    {
        var d = new string((code ?? string.Empty).Where(char.IsDigit).ToArray());
        return d.Length == 5;
    }

    private static string FormatPostalCode(string code)
    {
        var d = new string(code.Where(char.IsDigit).ToArray());
        return d.Length == 5 ? $"{d[..2]}-{d[2..]}" : code.Trim();
    }
}
