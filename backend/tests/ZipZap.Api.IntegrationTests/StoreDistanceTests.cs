using System.Net.Http.Json;
using FluentAssertions;
using Xunit;

namespace ZipZap.Api.IntegrationTests;

[Collection("api")]
public sealed class StoreDistanceTests
{
    private readonly ApiFactory _f;
    public StoreDistanceTests(ApiFactory f) => _f = f;

    private sealed record Store(Guid id, string name, double? latitude, double? longitude, double? distanceKm);

    private async Task<Guid> CreateStore(HttpClient c, double? lat, double? lng)
    {
        var resp = await c.PostAsJsonAsync("/api/catalog/stores", new
        {
            name = $"Dist {Guid.NewGuid():N}".Substring(0, 12),
            city = "Testowo", commissionRate = 0.10m, minimumOrderValue = 0m,
            latitude = lat, longitude = lng,
        });
        resp.EnsureSuccessStatusCode();
        return (await resp.Content.ReadFromJsonAsync<Store>())!.id;
    }

    [Fact]
    public async Task Stores_are_sorted_by_distance_with_distance_km_when_coordinates_given()
    {
        var admin = await _f.LoginAdminAsync();
        var c = _f.Authed(admin.accessToken);

        // Punkt odniesienia: centrum Krakowa.
        const double refLat = 50.0614, refLng = 19.9366;
        var near = await CreateStore(c, 50.0700, 19.9500); // ~1–2 km
        var far = await CreateStore(c, 52.2297, 21.0122);  // Warszawa, ~250 km
        var noCoords = await CreateStore(c, null, null);

        var all = (await _f.Anon().GetFromJsonAsync<Store[]>(
            $"/api/catalog/stores?onlyActive=false&lat={refLat.ToString(System.Globalization.CultureInfo.InvariantCulture)}&lng={refLng.ToString(System.Globalization.CultureInfo.InvariantCulture)}"))!;

        var nearS = all.Single(s => s.id == near);
        var farS = all.Single(s => s.id == far);
        var noS = all.Single(s => s.id == noCoords);

        nearS.distanceKm.Should().NotBeNull();
        nearS.distanceKm!.Value.Should().BeLessThan(5);
        farS.distanceKm!.Value.Should().BeGreaterThan(100);
        nearS.distanceKm!.Value.Should().BeLessThan(farS.distanceKm!.Value);
        noS.distanceKm.Should().BeNull();

        // Bliższy sklep występuje przed dalszym; sklep bez współrzędnych — po obu.
        Array.IndexOf(all, nearS).Should().BeLessThan(Array.IndexOf(all, farS));
        Array.IndexOf(all, farS).Should().BeLessThan(Array.IndexOf(all, noS));
    }

    [Fact]
    public async Task Without_coordinates_distance_is_null()
    {
        var admin = await _f.LoginAdminAsync();
        var c = _f.Authed(admin.accessToken);
        var id = await CreateStore(c, 50.0, 20.0);

        var all = await _f.Anon().GetFromJsonAsync<Store[]>("/api/catalog/stores?onlyActive=false");
        all!.Single(s => s.id == id).distanceKm.Should().BeNull();
    }
}
