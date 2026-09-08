using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using ZipZap.Modules.Payments.Application;

namespace ZipZap.Modules.Payments.Infrastructure;

/// <summary>
/// Dostawca testowy (dev): tworzy lokalną sesję i weryfikuje webhook podpisem
/// HMAC-SHA256 z sekretu. To NIE fałszywy sukces — autoryzacja wymaga poprawnie
/// podpisanego webhooka (jak u realnego dostawcy). Realny adapter zastąpi mocka.
/// </summary>
public sealed class MockPaymentProvider : IPaymentProvider
{
    private readonly PaymentsOptions _options;

    public MockPaymentProvider(IOptions<PaymentsOptions> options) => _options = options.Value;

    public string Key => "mock";

    public Task<PaymentSession> CreateSessionAsync(PaymentSessionRequest request, CancellationToken ct = default)
    {
        var sessionId = Guid.NewGuid().ToString("N");
        var redirect = $"{_options.PublicUrl.TrimEnd('/')}/pay/mock?session={sessionId}&payment={request.PaymentId}";
        return Task.FromResult(new PaymentSession(sessionId, redirect));
    }

    public WebhookResult VerifyWebhook(string rawBody, string? signature)
    {
        var expected = Sign(rawBody, _options.Mock.Secret);
        if (string.IsNullOrWhiteSpace(signature) ||
            !CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(expected), Encoding.UTF8.GetBytes(signature)))
        {
            return new WebhookResult(Verified: false, Guid.Empty, PaymentOutcome.Unknown, null);
        }

        try
        {
            using var doc = JsonDocument.Parse(rawBody);
            var root = doc.RootElement;
            var paymentId = Guid.Parse(root.GetProperty("paymentId").GetString()!);
            var outcomeStr = root.GetProperty("outcome").GetString();
            var providerRef = root.TryGetProperty("providerRef", out var pr) ? pr.GetString() : null;

            var outcome = outcomeStr?.ToLowerInvariant() switch
            {
                "authorized" => PaymentOutcome.Authorized,
                "failed" => PaymentOutcome.Failed,
                _ => PaymentOutcome.Unknown,
            };
            return new WebhookResult(Verified: true, paymentId, outcome, providerRef);
        }
        catch
        {
            return new WebhookResult(Verified: false, Guid.Empty, PaymentOutcome.Unknown, null);
        }
    }

    /// <summary>Podpis referencyjny (hex HMAC-SHA256) — używany też w dokumentacji/testach.</summary>
    public static string Sign(string rawBody, string secret)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        return Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes(rawBody)));
    }
}
