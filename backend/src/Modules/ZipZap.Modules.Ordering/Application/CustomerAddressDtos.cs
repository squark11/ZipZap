using ZipZap.Modules.Ordering.Domain;

namespace ZipZap.Modules.Ordering.Application;

public sealed record AddressDto(
    Guid Id, string Label, string Street, string BuildingNo, string? ApartmentNo,
    string PostalCode, string City, string? Notes, double? Latitude, double? Longitude, bool IsDefault)
{
    public static AddressDto From(CustomerAddress a) => new(
        a.Id, a.Label, a.Street, a.BuildingNo, a.ApartmentNo,
        a.PostalCode, a.City, a.Notes, a.Latitude, a.Longitude, a.IsDefault);
}

public sealed record AddressInput(
    string Label, string Street, string BuildingNo, string? ApartmentNo,
    string PostalCode, string City, string? Notes, double? Latitude, double? Longitude, bool IsDefault);
