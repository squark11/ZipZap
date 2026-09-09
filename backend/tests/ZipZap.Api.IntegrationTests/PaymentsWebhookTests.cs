using System.Net;
using System.Security.Cryptography;
using System.Text;
using FluentAssertions;
using Xunit;

namespace ZipZap.Api.IntegrationTests;

[Collection("api")]
public sealed class PaymentsWebhookTests
{
    private const string MockSecret = "mock-dev-secret";
    private readonly ApiFactory _f;
    public PaymentsWebhookTests(ApiFactory f) => _f = f;

    private static string Sign(string body)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(MockSecret));
        return Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes(body)));
    }

    private static HttpRequestMessage Webhook(string provider, string body, string signature)
    {
        var req = new HttpRequestMessage(HttpMethod.Post, $"/api/payments/webhook/{provider}")
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json"),
        };
        req.Headers.Add("X-Signature", signature);
        return req;
    }

    [Fact]
    public async Task Unknown_provider_is_not_found()
    {
        var resp = await _f.Anon().PostAsync("/api/payments/webhook/nonexistent",
            new StringContent("{}", Encoding.UTF8, "application/json"));
        resp.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Bad_signature_is_rejected()
    {
        var body = "{\"paymentId\":\"" + Guid.NewGuid() + "\",\"outcome\":\"authorized\"}";
        var resp = await _f.Anon().SendAsync(Webhook("mock", body, "deadbeef"));
        resp.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Valid_signature_unknown_payment_is_not_found()
    {
        // Poprawny podpis przechodzi weryfikację, ale płatność nie istnieje -> 404.
        var body = "{\"paymentId\":\"" + Guid.NewGuid() + "\",\"outcome\":\"authorized\"}";
        var resp = await _f.Anon().SendAsync(Webhook("mock", body, Sign(body)));
        resp.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
