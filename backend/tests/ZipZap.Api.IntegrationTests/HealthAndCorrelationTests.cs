using System.Net;
using FluentAssertions;
using Xunit;

namespace ZipZap.Api.IntegrationTests;

[Collection("api")]
public sealed class HealthAndCorrelationTests
{
    private readonly ApiFactory _f;
    public HealthAndCorrelationTests(ApiFactory f) => _f = f;

    [Fact]
    public async Task Live_returns_ok_with_correlation_header()
    {
        var resp = await _f.Anon().GetAsync("/health/live");
        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        resp.Headers.Contains("X-Correlation-Id").Should().BeTrue();
    }

    [Fact]
    public async Task Incoming_correlation_id_is_echoed()
    {
        var req = new HttpRequestMessage(HttpMethod.Get, "/health/live");
        req.Headers.Add("X-Correlation-Id", "test-cid-xyz");
        var resp = await _f.Anon().SendAsync(req);
        resp.Headers.GetValues("X-Correlation-Id").First().Should().Be("test-cid-xyz");
    }

    [Fact]
    public async Task Ready_reports_database_up()
    {
        var resp = await _f.Anon().GetAsync("/health/ready");
        resp.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
