using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Xunit;

namespace ZipZap.Api.IntegrationTests;

[Collection("api")]
public sealed class StoreProfileTests
{
    private readonly ApiFactory _f;
    public StoreProfileTests(ApiFactory f) => _f = f;

    private sealed record Store(Guid id, string name, string? logoUrl, double? latitude, double? longitude);

    [Fact]
    public async Task Create_with_logo_and_coordinates_then_get_returns_them()
    {
        var admin = await _f.LoginAdminAsync();
        var c = _f.Authed(admin.accessToken);

        var resp = await c.PostAsJsonAsync("/api/catalog/stores", new
        {
            name = $"Rapacz {Guid.NewGuid():N}".Substring(0, 16),
            city = "Kraków",
            commissionRate = 0.10m,
            minimumOrderValue = 0m,
            logoUrl = "https://cdn.zipzap.pl/rapacz.png",
            latitude = 50.0614,
            longitude = 19.9366,
        });
        resp.EnsureSuccessStatusCode();
        var created = (await resp.Content.ReadFromJsonAsync<Store>())!;

        var store = await _f.Anon().GetFromJsonAsync<Store>($"/api/catalog/stores/{created.id}");
        store!.logoUrl.Should().Be("https://cdn.zipzap.pl/rapacz.png");
        store.latitude.Should().BeApproximately(50.0614, 1e-6);
        store.longitude.Should().BeApproximately(19.9366, 1e-6);
    }

    [Fact]
    public async Task Patch_updates_logo_and_coordinates()
    {
        var admin = await _f.LoginAdminAsync();
        var storeId = await _f.CreateStoreAsync(admin.accessToken);
        var c = _f.Authed(admin.accessToken);

        (await c.PatchAsync($"/api/catalog/stores/{storeId}",
            JsonContent.Create(new { logoUrl = "https://cdn.zipzap.pl/logo2.png", latitude = 52.2297, longitude = 21.0122 })))
            .EnsureSuccessStatusCode();

        var store = await _f.Anon().GetFromJsonAsync<Store>($"/api/catalog/stores/{storeId}");
        store!.logoUrl.Should().Be("https://cdn.zipzap.pl/logo2.png");
        store.latitude.Should().BeApproximately(52.2297, 1e-6);
    }

    [Fact]
    public async Task Patch_rejects_out_of_range_latitude()
    {
        var admin = await _f.LoginAdminAsync();
        var storeId = await _f.CreateStoreAsync(admin.accessToken);
        var c = _f.Authed(admin.accessToken);

        var resp = await c.PatchAsync($"/api/catalog/stores/{storeId}",
            JsonContent.Create(new { latitude = 95.0, longitude = 19.0 }));
        resp.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
