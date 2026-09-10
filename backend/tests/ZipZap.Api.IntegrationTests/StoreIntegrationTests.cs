using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Xunit;

namespace ZipZap.Api.IntegrationTests;

[Collection("api")]
public sealed class StoreIntegrationTests
{
    private readonly ApiFactory _f;
    public StoreIntegrationTests(ApiFactory f) => _f = f;

    private sealed record Status(string provider, string? merchantId, string? posId,
        bool sandbox, bool hasApiKey, bool hasCrcKey);

    [Fact]
    public async Task Put_encrypts_secrets_and_get_returns_only_flags()
    {
        var admin = await _f.LoginAdminAsync();
        var storeId = await _f.CreateStoreAsync(admin.accessToken);
        var c = _f.Authed(admin.accessToken);

        var put = await c.PutAsJsonAsync($"/api/payments/stores/{storeId}/integration", new
        {
            provider = "przelewy24", merchantId = "M1", posId = "P1", sandbox = true,
            apiKey = "SUPER-SECRET-KEY", crcKey = "SUPER-SECRET-CRC",
        });
        put.EnsureSuccessStatusCode();

        // Odpowiedź NIE może zawierać sekretów.
        var putRaw = await put.Content.ReadAsStringAsync();
        putRaw.Should().NotContain("SUPER-SECRET");

        var get = await c.GetAsync($"/api/payments/stores/{storeId}/integration");
        var getRaw = await get.Content.ReadAsStringAsync();
        getRaw.Should().NotContain("SUPER-SECRET");

        var dto = await get.Content.ReadFromJsonAsync<Status>();
        dto!.provider.Should().Be("przelewy24");
        dto.hasApiKey.Should().BeTrue();
        dto.hasCrcKey.Should().BeTrue();
    }

    [Fact]
    public async Task Empty_secret_keeps_existing_one()
    {
        var admin = await _f.LoginAdminAsync();
        var storeId = await _f.CreateStoreAsync(admin.accessToken);
        var c = _f.Authed(admin.accessToken);

        await c.PutAsJsonAsync($"/api/payments/stores/{storeId}/integration", new
        {
            provider = "stripe", sandbox = false, apiKey = "KEY-A", crcKey = "",
        });

        // Aktualizacja bez sekretu — klucz ma pozostać.
        var put2 = await c.PutAsJsonAsync($"/api/payments/stores/{storeId}/integration", new
        {
            provider = "stripe", merchantId = "M2", sandbox = false, apiKey = "", crcKey = "",
        });
        var dto = await put2.Content.ReadFromJsonAsync<Status>();
        dto!.hasApiKey.Should().BeTrue();
        dto.merchantId.Should().Be("M2");
    }

    [Fact]
    public async Task Employee_of_another_store_is_forbidden()
    {
        var admin = await _f.LoginAdminAsync();
        var storeA = await _f.CreateStoreAsync(admin.accessToken);
        var storeB = await _f.CreateStoreAsync(admin.accessToken);
        var empB = await _f.CreateEmployeeAsync(admin.accessToken, storeB);

        var resp = await _f.Authed(empB.accessToken)
            .GetAsync($"/api/payments/stores/{storeA}/integration");
        resp.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}
