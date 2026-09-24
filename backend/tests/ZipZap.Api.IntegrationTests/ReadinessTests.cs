using System.Net;
using FluentAssertions;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using ZipZap.BuildingBlocks.Persistence;

namespace ZipZap.Api.IntegrationTests;

/// <summary>
/// Gotowość usługi: nieudana migracja modułu nie może skutkować zgłoszeniem gotowości
/// (proces żyje — /health 200 — ale /health/ready 503).
/// </summary>
[Collection("api")]
public sealed class ReadinessTests
{
    private readonly ApiFactory _f;
    public ReadinessTests(ApiFactory f) => _f = f;

    private sealed class FailingMigrator : IModuleDbMigrator
    {
        public string ModuleName => "BrokenModule";
        public Task MigrateAsync(CancellationToken ct = default) => throw new InvalidOperationException("migration boom");
    }

    [Fact]
    public async Task Ready_in_plain_dev_when_migrations_succeed()
        => (await _f.Anon().GetAsync("/health/ready")).StatusCode.Should().Be(HttpStatusCode.OK);

    [Fact]
    public async Task Not_ready_when_a_module_migration_fails()
    {
        using var broken = _f.WithWebHostBuilder(b => b.ConfigureTestServices(s =>
            s.AddScoped<IModuleDbMigrator, FailingMigrator>()));
        var client = broken.CreateClient();

        (await client.GetAsync("/health")).StatusCode.Should().Be(HttpStatusCode.OK);
        var ready = await client.GetAsync("/health/ready");
        ready.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
        (await ready.Content.ReadAsStringAsync()).Should().NotContain("boom", "szczegóły błędu tylko w logach");
    }
}
