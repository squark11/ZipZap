using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Xunit;

namespace ZipZap.Api.IntegrationTests;

[Collection("api")]
public sealed class AuthAndAuthorizationTests
{
    private readonly ApiFactory _f;
    public AuthAndAuthorizationTests(ApiFactory f) => _f = f;

    [Fact]
    public async Task Register_then_me_returns_ok()
    {
        var auth = await _f.RegisterCustomerAsync();
        var resp = await _f.Authed(auth.accessToken).GetAsync("/api/identity/me");
        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        auth.user.roles.Should().Contain("Customer");
    }

    [Fact]
    public async Task Me_requires_authentication()
    {
        var resp = await _f.Anon().GetAsync("/api/identity/me");
        resp.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Login_with_wrong_password_is_unauthorized()
    {
        var auth = await _f.RegisterCustomerAsync();
        var resp = await _f.Anon().PostAsJsonAsync("/api/identity/login",
            new { email = auth.user.email, password = "definitely-wrong" });
        resp.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Public_store_listing_is_anonymous()
    {
        var resp = await _f.Anon().GetAsync("/api/catalog/stores");
        resp.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Create_store_requires_authentication()
    {
        var resp = await _f.Anon().PostAsJsonAsync("/api/catalog/stores",
            new { name = "X", city = "Y", commissionRate = 0.1m, minimumOrderValue = 0m });
        resp.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Create_store_forbidden_for_customer()
    {
        var auth = await _f.RegisterCustomerAsync();
        var resp = await _f.Authed(auth.accessToken).PostAsJsonAsync("/api/catalog/stores",
            new { name = "X", city = "Y", commissionRate = 0.1m, minimumOrderValue = 0m });
        resp.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Create_store_allowed_for_admin()
    {
        var admin = await _f.LoginAdminAsync();
        var resp = await _f.Authed(admin.accessToken).PostAsJsonAsync("/api/catalog/stores",
            new { name = $"IT {Guid.NewGuid():N}".Substring(0, 12), city = "Y", commissionRate = 0.1m, minimumOrderValue = 0m });
        resp.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
