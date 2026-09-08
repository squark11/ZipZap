using FluentAssertions;
using Microsoft.Extensions.Options;
using Xunit;
using ZipZap.Modules.Payments.Application;
using ZipZap.Modules.Payments.Infrastructure;

namespace ZipZap.Modules.Payments.Tests;

public class MockPaymentProviderTests
{
    private static MockPaymentProvider Provider(string secret = "test-secret")
        => new(Options.Create(new PaymentsOptions
        {
            PublicUrl = "http://localhost:4200",
            Mock = new PaymentsOptions.MockProviderOptions { Secret = secret },
        }));

    [Fact]
    public void Valid_signature_parses_authorized_outcome()
    {
        var provider = Provider("secret1");
        var paymentId = Guid.NewGuid();
        var body = $"{{\"paymentId\":\"{paymentId}\",\"outcome\":\"authorized\",\"providerRef\":\"REF-1\"}}";
        var signature = MockPaymentProvider.Sign(body, "secret1");

        var result = provider.VerifyWebhook(body, signature);

        result.Verified.Should().BeTrue();
        result.PaymentId.Should().Be(paymentId);
        result.Outcome.Should().Be(PaymentOutcome.Authorized);
        result.ProviderRef.Should().Be("REF-1");
    }

    [Fact]
    public void Wrong_signature_is_rejected()
    {
        var provider = Provider("secret1");
        var body = $"{{\"paymentId\":\"{Guid.NewGuid()}\",\"outcome\":\"authorized\"}}";

        var result = provider.VerifyWebhook(body, "not-the-right-signature");

        result.Verified.Should().BeFalse();
    }

    [Fact]
    public void Signature_from_different_secret_is_rejected()
    {
        var provider = Provider("secret1");
        var body = $"{{\"paymentId\":\"{Guid.NewGuid()}\",\"outcome\":\"failed\"}}";
        var signatureFromOtherSecret = MockPaymentProvider.Sign(body, "secret2");

        provider.VerifyWebhook(body, signatureFromOtherSecret).Verified.Should().BeFalse();
    }

    [Fact]
    public async Task CreateSession_returns_redirect_with_session()
    {
        var provider = Provider();
        var session = await provider.CreateSessionAsync(
            new PaymentSessionRequest(Guid.NewGuid(), Guid.NewGuid(), 12.34m, "PLN", "test"));

        session.SessionId.Should().NotBeNullOrWhiteSpace();
        session.RedirectUrl.Should().Contain("session=");
    }
}
