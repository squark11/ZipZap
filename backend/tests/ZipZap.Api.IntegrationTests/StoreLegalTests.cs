using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Xunit;

namespace ZipZap.Api.IntegrationTests;

[Collection("api")]
public sealed class StoreLegalTests
{
    private readonly ApiFactory _f;
    public StoreLegalTests(ApiFactory f) => _f = f;

    private sealed record Legal(string? termsUrl, string? privacyUrl, string? gdprUrl, bool requiresAcceptance);

    [Fact]
    public async Task Get_legal_is_public_and_defaults_empty()
    {
        var storeId = Guid.NewGuid();
        var legal = await _f.Anon().GetFromJsonAsync<Legal>($"/api/stores/{storeId}/legal");
        legal!.requiresAcceptance.Should().BeFalse();
        legal.termsUrl.Should().BeNull();
        legal.privacyUrl.Should().BeNull();
    }

    [Fact]
    public async Task Put_then_get_roundtrips()
    {
        var admin = await _f.LoginAdminAsync();
        var storeId = Guid.Parse(await _f.CreateStoreAsync(admin.accessToken));
        var c = _f.Authed(admin.accessToken);

        var body = new Legal("https://sklep.pl/regulamin", "https://sklep.pl/prywatnosc", "https://sklep.pl/rodo", true);
        var put = await c.PutAsJsonAsync($"/api/stores/{storeId}/legal", body);
        put.EnsureSuccessStatusCode();

        var legal = await _f.Anon().GetFromJsonAsync<Legal>($"/api/stores/{storeId}/legal");
        legal!.requiresAcceptance.Should().BeTrue();
        legal.termsUrl.Should().Be("https://sklep.pl/regulamin");
        legal.privacyUrl.Should().Be("https://sklep.pl/prywatnosc");
        legal.gdprUrl.Should().Be("https://sklep.pl/rodo");
    }

    [Fact]
    public async Task Put_requires_urls_when_acceptance_required()
    {
        var admin = await _f.LoginAdminAsync();
        var storeId = Guid.Parse(await _f.CreateStoreAsync(admin.accessToken));
        var c = _f.Authed(admin.accessToken);

        var body = new Legal(null, null, null, true); // wymóg akceptacji bez dokumentów
        var put = await c.PutAsJsonAsync($"/api/stores/{storeId}/legal", body);
        put.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Put_rejects_invalid_url()
    {
        var admin = await _f.LoginAdminAsync();
        var storeId = Guid.Parse(await _f.CreateStoreAsync(admin.accessToken));
        var c = _f.Authed(admin.accessToken);

        var body = new Legal("nie-jest-urlem", "https://sklep.pl/p", null, false);
        var put = await c.PutAsJsonAsync($"/api/stores/{storeId}/legal", body);
        put.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Other_store_employee_cannot_edit_legal()
    {
        var admin = await _f.LoginAdminAsync();
        var storeA = Guid.Parse(await _f.CreateStoreAsync(admin.accessToken));
        var storeB = Guid.Parse(await _f.CreateStoreAsync(admin.accessToken));
        var empB = await _f.CreateEmployeeAsync(admin.accessToken, storeB.ToString());

        var body = new Legal("https://x.pl/r", "https://x.pl/p", null, true);
        var put = await _f.Authed(empB.accessToken).PutAsJsonAsync($"/api/stores/{storeA}/legal", body);
        put.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}
