using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Xunit;

namespace ZipZap.Api.IntegrationTests;

[Collection("api")]
public sealed class CaptchaConfigTests
{
    private readonly ApiFactory _f;
    public CaptchaConfigTests(ApiFactory f) => _f = f;

    private sealed record Status(string? googleClientId, string? captchaProvider, string? captchaSiteKey, bool hasCaptchaSecret);
    private sealed record PublicCfg(string? captchaProvider, string? captchaSiteKey);

    private const string SiteKey = "1x00000000000000000000AA";
    private const string Secret = "2x0000000000000000000000000000000AA";

    [Fact]
    public async Task Configure_captcha_write_only_secret_and_public_site_key()
    {
        var admin = await _f.LoginAdminAsync();
        var c = _f.Authed(admin.accessToken);
        try
        {
            var put = await c.PutAsJsonAsync("/api/admin/config/integrations",
                new { captchaProvider = "turnstile", captchaSiteKey = SiteKey, captchaSecret = Secret });
            put.EnsureSuccessStatusCode();

            // Status: flagi ustawione, sekret NIE zwracany.
            var raw = await c.GetStringAsync("/api/admin/config/integrations");
            raw.Should().NotContain(Secret);
            var status = System.Text.Json.JsonSerializer.Deserialize<Status>(raw,
                new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web))!;
            status.captchaProvider.Should().Be("turnstile");
            status.captchaSiteKey.Should().Be(SiteKey);
            status.hasCaptchaSecret.Should().BeTrue();

            // Publiczna konfiguracja udostępnia site key (jawny) do renderu widgetu.
            var pub = await _f.Anon().GetFromJsonAsync<PublicCfg>("/api/config/public");
            pub!.captchaProvider.Should().Be("turnstile");
            pub.captchaSiteKey.Should().Be(SiteKey);
        }
        finally
        {
            // Wyłącz captchę, by nie wpływała na pozostałe testy (rejestracja/uwagi bez tokenu).
            await c.PutAsJsonAsync("/api/admin/config/integrations", new { captchaProvider = "" });
            var pub = await _f.Anon().GetFromJsonAsync<PublicCfg>("/api/config/public");
            pub!.captchaSiteKey.Should().BeNull();
        }
    }

    [Fact]
    public async Task Enabling_captcha_requires_site_key()
    {
        var admin = await _f.LoginAdminAsync();
        var resp = await _f.Authed(admin.accessToken)
            .PutAsJsonAsync("/api/admin/config/integrations", new { captchaProvider = "turnstile" });
        resp.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Registration_and_feedback_work_without_token_when_captcha_disabled()
    {
        // Domyślnie captcha wyłączona → publiczne formularze działają bez tokenu.
        var reg = await _f.Anon().PostAsJsonAsync("/api/identity/register",
            new { email = $"cap-{Guid.NewGuid():N}@test.pl", password = "Passw0rd!", fullName = "Cap User" });
        reg.EnsureSuccessStatusCode();

        var fb = await _f.Anon().PostAsJsonAsync("/api/feedback", new { type = "Other", message = "Bez captchy dziala" });
        fb.EnsureSuccessStatusCode();
    }
}
