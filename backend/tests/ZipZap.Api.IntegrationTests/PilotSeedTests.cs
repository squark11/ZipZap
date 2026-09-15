using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Xunit;

namespace ZipZap.Api.IntegrationTests;

[Collection("api")]
public sealed class PilotSeedTests
{
    private readonly ApiFactory _f;
    public PilotSeedTests(ApiFactory f) => _f = f;

    private sealed record Store(Guid id, string slug, string? logoUrl, double? latitude);
    private sealed record Product(string name, string? imageUrl);

    [Fact]
    public async Task Seed_creates_demo_stores_with_logo_and_photo_products()
    {
        var admin = await _f.LoginAdminAsync();
        var c = _f.Authed(admin.accessToken);

        (await c.PostAsJsonAsync("/api/admin/seed/pilot", new { })).EnsureSuccessStatusCode();

        var store = await _f.Anon().GetFromJsonAsync<Store>("/api/catalog/stores/demo-rapacz-rynek");
        store!.logoUrl.Should().Contain("/mock/stores/rapacz.png");
        store.latitude.Should().NotBeNull();

        var products = await _f.Anon().GetFromJsonAsync<Product[]>($"/api/catalog/stores/{store.id}/products");
        products!.Length.Should().BeGreaterThanOrEqualTo(4);
        products.Should().Contain(p => p.imageUrl != null && p.imageUrl.Contains("/mock/food/"));
    }

    [Fact]
    public async Task Seed_is_idempotent()
    {
        var admin = await _f.LoginAdminAsync();
        var c = _f.Authed(admin.accessToken);

        (await c.PostAsJsonAsync("/api/admin/seed/pilot", new { })).EnsureSuccessStatusCode();
        (await c.PostAsJsonAsync("/api/admin/seed/pilot", new { })).EnsureSuccessStatusCode();

        // Slug jest unikalny — brak duplikatu sklepu demo.
        var all = await _f.Anon().GetFromJsonAsync<Store[]>("/api/catalog/stores?onlyActive=false");
        all!.Count(s => s.slug == "demo-rapacz-rynek").Should().Be(1);
    }

    [Fact]
    public async Task Non_admin_cannot_seed()
    {
        var customer = await _f.RegisterCustomerAsync();
        var resp = await _f.Authed(customer.accessToken).PostAsJsonAsync("/api/admin/seed/pilot", new { });
        resp.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}
