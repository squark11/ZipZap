using System.Net;
using System.Text;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using ZipZap.Api.Configuration;
using ZipZap.Modules.Identity.Application;
using ZipZap.Modules.Payments.Application;

namespace ZipZap.Api.IntegrationTests;

/// <summary>
/// Kontrole pozytywne dla testów trybu publicznego: w czystym dev te same ścieżki DZIAŁAJĄ,
/// więc 404 w pilotażu wynika z hartowania, a nie z błędnej ścieżki w teście.
/// </summary>
[Collection("api")]
public sealed class DevModeControlTests
{
    private readonly ApiFactory _f;
    public DevModeControlTests(ApiFactory f) => _f = f;

    [Fact]
    public async Task Swagger_is_available_in_plain_dev()
        => (await _f.Anon().GetAsync("/swagger/v1/swagger.json")).StatusCode.Should().Be(HttpStatusCode.OK);

    [Fact]
    public async Task Mock_webhook_exists_in_plain_dev_and_rejects_bad_signature()
    {
        var req = new HttpRequestMessage(HttpMethod.Post, "/api/payments/webhook/mock")
        { Content = new StringContent("{}", Encoding.UTF8, "application/json") };
        req.Headers.Add("X-Signature", "garbage");
        // 400 invalid_signature (weryfikator istnieje) — w pilotażu ta sama ścieżka daje 404.
        (await _f.Anon().SendAsync(req)).StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public void Mock_provider_is_registered_in_plain_dev()
    {
        using var scope = _f.Services.CreateScope();
        scope.ServiceProvider.GetServices<IPaymentProvider>().Should().Contain(p => p.Key == "mock");
    }

    [Fact]
    public void Host_uses_http_email_sender_not_the_logging_mock()
    {
        // Moduł Identity rejestruje LoggingEmailSender jako domyślny; host musi go nadpisać.
        using var scope = _f.Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<IEmailSender>().Should().BeOfType<HttpEmailSender>();
    }
}
