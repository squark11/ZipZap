using System.Net;
using System.Net.Http.Json;
using System.Text;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using ZipZap.Modules.Identity;
using ZipZap.Modules.Payments.Application;
using ZipZap.Modules.Payments.Infrastructure;

namespace ZipZap.Api.IntegrationTests;

/// <summary>
/// Tryb publicznego pilotażu (Pilot:Public=true) — mimo ASPNETCORE_ENVIRONMENT=Development
/// aplikacja nie wystawia narzędzi deweloperskich ani mockowego przepływu płatności.
/// </summary>
[Collection("pilot")]
public sealed class PilotPublicModeTests
{
    private readonly PilotApiFactory _p;
    public PilotPublicModeTests(PilotApiFactory p) => _p = p;

    [Fact]
    public async Task Swagger_is_not_exposed()
    {
        (await _p.Anon().GetAsync("/swagger/v1/swagger.json")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await _p.Anon().GetAsync("/swagger/index.html")).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Default_admin_is_not_seeded_even_if_seed_password_is_configured()
    {
        // appsettings.Development.json zawiera Seed:AdminPassword — w trybie hartowanym seed jest pomijany.
        var resp = await _p.Anon().PostAsJsonAsync("/api/identity/login",
            new { email = "admin@zipzap.local", password = "Admin123!" });
        resp.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Mock_payment_pages_are_not_available()
    {
        (await _p.Anon().GetAsync($"/api/payments/mock/pay?payment={Guid.NewGuid()}"))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);

        var form = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["payment"] = Guid.NewGuid().ToString(), ["outcome"] = "authorized",
        });
        (await _p.Anon().PostAsync("/api/payments/mock/complete", form))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Mock_webhook_is_rejected_even_with_valid_repo_secret_signature()
    {
        // Podpis wykonany jawnym w repozytorium sekretem `mock-dev-secret` — w pilotażu nie ma
        // weryfikatora mocka, więc webhook nie może autoryzować żadnej płatności.
        var body = $$"""{"paymentId":"{{Guid.NewGuid()}}","outcome":"authorized","providerRef":"forged"}""";
        foreach (var signature in new[] { MockPaymentProvider.Sign(body, "mock-dev-secret"), "garbage" })
        {
            var req = new HttpRequestMessage(HttpMethod.Post, "/api/payments/webhook/mock")
            { Content = new StringContent(body, Encoding.UTF8, "application/json") };
            req.Headers.Add("X-Signature", signature);
            (await _p.Anon().SendAsync(req)).StatusCode.Should().Be(HttpStatusCode.NotFound);
        }
    }

    [Fact]
    public void Mock_payment_provider_is_not_registered()
    {
        using var scope = _p.Services.CreateScope();
        var providers = scope.ServiceProvider.GetServices<IPaymentProvider>();
        providers.Should().NotContain(pr => pr.Key == "mock");
        scope.ServiceProvider.GetRequiredService<PaymentProviderRegistry>().DefaultOrNull.Should().BeNull();
    }

    [Fact]
    public async Task Cors_allows_only_configured_origin()
    {
        async Task<HttpResponseMessage> Preflight(string origin)
        {
            var req = new HttpRequestMessage(HttpMethod.Options, "/api/catalog/stores");
            req.Headers.Add("Origin", origin);
            req.Headers.Add("Access-Control-Request-Method", "GET");
            return await _p.Anon().SendAsync(req);
        }

        var allowed = await Preflight(PilotApiFactory.AllowedOrigin);
        allowed.Headers.TryGetValues("Access-Control-Allow-Origin", out var acao).Should().BeTrue();
        acao!.Should().ContainSingle().Which.Should().Be(PilotApiFactory.AllowedOrigin);

        var evil = await Preflight("https://evil.example");
        evil.Headers.Contains("Access-Control-Allow-Origin").Should().BeFalse();
    }

    [Fact]
    public async Task Rate_limiter_is_active_in_public_mode()
    {
        var c = _p.ClientFromIp("203.0.113.40");
        var codes = new List<HttpStatusCode>();
        for (var i = 0; i < 11; i++)
            codes.Add((await c.PostAsJsonAsync("/api/identity/login", new { email = "x@test.pl", password = "bad" })).StatusCode);
        codes.Take(10).Should().NotContain(HttpStatusCode.TooManyRequests);
        codes.Last().Should().Be(HttpStatusCode.TooManyRequests);
    }

    [Fact]
    public async Task Ready_when_migrations_ok_and_no_admin_has_default_password()
    {
        var resp = await _p.Anon().GetAsync("/health/ready");
        resp.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Not_ready_while_an_admin_account_keeps_a_default_password()
    {
        // Osobna świeża baza: start → migracja; dopisujemy admina z domyślnym hasłem (np. konto
        // pozostałe po wcześniejszym seedzie deweloperskim) → ponowny start zgłasza brak gotowości.
        using var weak = new PilotApiFactory("zipzap_it_pilot_weak");
        (await weak.Anon().GetAsync("/health/ready")).StatusCode.Should().Be(HttpStatusCode.OK);

        await IdentityModule.SeedDevelopmentAdminAsync(weak.Services, "legacy-admin@test.pl", "Admin123!");

        using var restarted = weak.WithWebHostBuilder(_ => { });
        var resp = await restarted.CreateClient().GetAsync("/health/ready");
        resp.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
        var body = await resp.Content.ReadAsStringAsync();
        body.Should().Contain("not-ready");
        // Publiczna odpowiedź nie ujawnia szczegółów (np. że istnieje konto z domyślnym hasłem).
        body.Should().NotContainAny("admin", "Admin123!", "password", "legacy-admin");
    }

    [Theory]
    [InlineData("Jwt:SigningKey", "CHANGE_ME_DEV_ONLY_super_secret_key_at_least_32_chars_long", "Jwt:SigningKey")]
    [InlineData("RateLimiting:Enabled", "false", "RateLimiting:Enabled")]
    [InlineData("Pilot:AdminPasswordConfirmed", "false", "Pilot:AdminPasswordConfirmed")]
    public void Startup_fails_fast_on_unsafe_public_configuration(string key, string value, string expected)
    {
        using var bad = new PilotApiFactory("zipzap_it_pilot_guard",
            b => b.UseSetting(key, value), recreateDatabase: false);
        var ex = Record.Exception(() => bad.CreateClient());
        ex.Should().NotBeNull();
        ex!.ToString().Should().Contain(expected);
    }
}
