using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Xunit;

namespace ZipZap.Api.IntegrationTests;

/// <summary>
/// Limiter nadużyć (10 żądań POST / minutę / IP klienta) na publicznych endpointach.
/// Każdy test ma własny adres z puli TEST-NET-3, więc testy nie dzielą limitu.
/// </summary>
[Collection("api")]
public sealed class RateLimitingTests
{
    private const int Limit = 10; // domyślny RateLimiting:PermitPerMinute
    private readonly ApiFactory _f;
    public RateLimitingTests(ApiFactory f) => _f = f;

    private static async Task<List<HttpStatusCode>> FireAsync(Func<Task<HttpResponseMessage>> send, int count)
    {
        var codes = new List<HttpStatusCode>();
        for (var i = 0; i < count; i++) codes.Add((await send()).StatusCode);
        return codes;
    }

    private static void AssertThrottledOnlyAfterLimit(List<HttpStatusCode> codes)
    {
        codes.Take(Limit).Should().NotContain(HttpStatusCode.TooManyRequests, "pierwsze {0} żądań mieści się w limicie", Limit);
        codes.Last().Should().Be(HttpStatusCode.TooManyRequests);
    }

    [Fact]
    public async Task Login_is_throttled_after_limit()
    {
        var c = _f.ClientFromIp("203.0.113.11");
        var codes = await FireAsync(() => c.PostAsJsonAsync("/api/identity/login",
            new { email = "nobody@test.pl", password = "wrong" }), Limit + 1);
        AssertThrottledOnlyAfterLimit(codes);
    }

    [Fact]
    public async Task Register_is_throttled_after_limit()
    {
        var c = _f.ClientFromIp("203.0.113.12");
        // Niepoprawny e-mail → 400 bez tworzenia konta; limiter liczy każde żądanie.
        var codes = await FireAsync(() => c.PostAsJsonAsync("/api/identity/register",
            new { email = "not-an-email", password = "Passw0rd!", fullName = "X" }), Limit + 1);
        AssertThrottledOnlyAfterLimit(codes);
    }

    [Fact]
    public async Task Password_forgot_is_throttled_after_limit()
    {
        var c = _f.ClientFromIp("203.0.113.13");
        var codes = await FireAsync(() => c.PostAsJsonAsync("/api/identity/password/forgot",
            new { email = "nobody@test.pl" }), Limit + 1);
        AssertThrottledOnlyAfterLimit(codes);
    }

    [Fact]
    public async Task Password_reset_is_throttled_after_limit()
    {
        var c = _f.ClientFromIp("203.0.113.14");
        var codes = await FireAsync(() => c.PostAsJsonAsync("/api/identity/password/reset",
            new { token = "invalid", newPassword = "Passw0rd!" }), Limit + 1);
        AssertThrottledOnlyAfterLimit(codes);
    }

    [Fact]
    public async Task Feedback_is_throttled_after_limit()
    {
        var c = _f.ClientFromIp("203.0.113.15");
        var codes = await FireAsync(() => c.PostAsJsonAsync("/api/feedback",
            new { type = "Other", message = "rate-limit test" }), Limit + 1);
        AssertThrottledOnlyAfterLimit(codes);
    }

    [Fact]
    public async Task Spoofed_forwarded_for_entries_do_not_bypass_the_limit()
    {
        // Klient dopisuje dowolne adresy po LEWEJ; proxy hostingu dopisuje prawdziwy adres na końcu.
        // Limiter bierze tylko ostatni (zaufany) wpis → każda próba trafia do tego samego limitu.
        const string realClient = "203.0.113.16";
        var codes = new List<HttpStatusCode>();
        for (var i = 0; i < Limit + 1; i++)
        {
            var spoofed = $"198.51.100.{i + 1}, {realClient}";
            var resp = await _f.ClientFromIp(spoofed).PostAsJsonAsync("/api/identity/login",
                new { email = "nobody@test.pl", password = "wrong" });
            codes.Add(resp.StatusCode);
        }
        AssertThrottledOnlyAfterLimit(codes);
    }

    [Fact]
    public async Task Limit_is_per_client_ip_not_global()
    {
        // Wyczerpanie limitu jednego klienta nie blokuje innego (partycja po IP klienta).
        var a = _f.ClientFromIp("203.0.113.17");
        (await FireAsync(() => a.PostAsJsonAsync("/api/identity/login",
            new { email = "nobody@test.pl", password = "wrong" }), Limit + 1)).Last()
            .Should().Be(HttpStatusCode.TooManyRequests);

        var b = await _f.ClientFromIp("203.0.113.18").PostAsJsonAsync("/api/identity/login",
            new { email = "nobody@test.pl", password = "wrong" });
        b.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
