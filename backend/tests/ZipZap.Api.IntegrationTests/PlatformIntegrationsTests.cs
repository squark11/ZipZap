using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Xunit;

namespace ZipZap.Api.IntegrationTests;

[Collection("api")]
public sealed class PlatformIntegrationsTests
{
    private readonly ApiFactory _f;
    public PlatformIntegrationsTests(ApiFactory f) => _f = f;

    private sealed record Integrations(string? googleClientId);
    private sealed record PublicConfig(string? googleClientId, bool googleSignInEnabled);
    private sealed record Status(bool googleSignIn);

    [Fact]
    public async Task Admin_sets_google_client_id_visible_in_admin_public_and_status()
    {
        var admin = await _f.LoginAdminAsync();
        var c = _f.Authed(admin.accessToken);
        var gid = $"it-{Guid.NewGuid():N}.apps.googleusercontent.com";

        (await c.PutAsJsonAsync("/api/admin/config/integrations", new { googleClientId = gid }))
            .EnsureSuccessStatusCode();

        (await c.GetFromJsonAsync<Integrations>("/api/admin/config/integrations"))!
            .googleClientId.Should().Be(gid);

        // Publiczny endpoint dla aplikacji (jawny Client ID → serverClientId).
        var pub = await _f.Anon().GetFromJsonAsync<PublicConfig>("/api/config/public");
        pub!.googleClientId.Should().Be(gid);
        pub.googleSignInEnabled.Should().BeTrue();

        // Status w panelu odzwierciedla efektywną wartość.
        (await c.GetFromJsonAsync<Status>("/api/admin/config/status"))!.googleSignIn.Should().BeTrue();
    }

    [Fact]
    public async Task Put_rejects_malformed_google_client_id()
    {
        var admin = await _f.LoginAdminAsync();
        var resp = await _f.Authed(admin.accessToken)
            .PutAsJsonAsync("/api/admin/config/integrations", new { googleClientId = "nie-jest-clientem" });
        resp.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Store_employee_cannot_edit_platform_integrations()
    {
        var admin = await _f.LoginAdminAsync();
        var storeId = await _f.CreateStoreAsync(admin.accessToken);
        var emp = await _f.CreateEmployeeAsync(admin.accessToken, storeId);

        var resp = await _f.Authed(emp.accessToken)
            .PutAsJsonAsync("/api/admin/config/integrations", new { googleClientId = "x.apps.googleusercontent.com" });
        resp.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}
