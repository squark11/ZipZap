using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Xunit;
using ZipZap.Api.Security;

namespace ZipZap.Api.IntegrationTests;

/// <summary>Reguły fail-fast (bez hosta i bazy).</summary>
public sealed class HardeningGuardTests
{
    private const string StrongJwt = "a-random-looking-signing-key-0123456789-abcdefghij";
    private const string StrongDb = "Host=db;Database=app;Username=app;Password=S3cure-Db-Pass!";

    private static IConfiguration Cfg(params (string Key, string? Value)[] pairs) =>
        new ConfigurationBuilder().AddInMemoryCollection(pairs.ToDictionary(p => p.Key, p => p.Value)).Build();

    private static (string, string?)[] SafePilot(params (string, string?)[] overrides)
    {
        var d = new Dictionary<string, string?>
        {
            ["Pilot:Public"] = "true",
            ["Pilot:AdminPasswordConfirmed"] = "true",
            ["Jwt:SigningKey"] = StrongJwt,
            ["ConnectionStrings:Postgres"] = StrongDb,
        };
        foreach (var (k, v) in overrides) d[k] = v;
        return d.Select(kv => (kv.Key, kv.Value)).ToArray();
    }

    [Fact]
    public void Plain_dev_is_not_guarded()
        => HardeningGuard.Check(Cfg(("Jwt:SigningKey", "CHANGE_ME_DEV_ONLY_x")), isProduction: false).Should().BeEmpty();

    [Fact]
    public void Safe_public_pilot_passes_without_real_payment_provider()
        => HardeningGuard.Check(Cfg(SafePilot()), isProduction: false).Should().BeEmpty();

    [Theory]
    [InlineData("Jwt:SigningKey", "CHANGE_ME_DEV_ONLY_super_secret_key_at_least_32_chars_long", "Jwt:SigningKey")]
    [InlineData("Jwt:SigningKey", "short-key", "Jwt:SigningKey")]
    [InlineData("ConnectionStrings:Postgres", "Host=db;Username=zipzap;Password=zipzap", "ConnectionStrings:Postgres")]
    [InlineData("ConnectionStrings:Postgres", "Host=db;Username=zipzap;Password = zipzap", "ConnectionStrings:Postgres")]
    [InlineData("Pilot:AdminPasswordConfirmed", "false", "Pilot:AdminPasswordConfirmed")]
    [InlineData("RateLimiting:Enabled", "false", "RateLimiting:Enabled")]
    public void Unsafe_public_pilot_is_rejected(string key, string value, string expected)
        => HardeningGuard.Check(Cfg(SafePilot((key, value))), isProduction: false)
            .Should().Contain(p => p.StartsWith(expected));

    [Fact]
    public void Rabbit_password_required_only_when_broker_is_used()
    {
        HardeningGuard.Check(Cfg(SafePilot(("RabbitMq:Password", "zipzap"))), false).Should().BeEmpty();
        HardeningGuard.Check(Cfg(SafePilot(("RabbitMq:Host", "mq"), ("RabbitMq:Password", "zipzap"))), false)
            .Should().Contain(p => p.StartsWith("RabbitMq:Password"));
    }

    [Fact]
    public void Production_requires_real_provider_and_rejects_repo_mock_secret()
    {
        var problems = HardeningGuard.Check(Cfg(
            ("Jwt:SigningKey", StrongJwt), ("ConnectionStrings:Postgres", StrongDb),
            ("RabbitMq:Password", "strong-mq-pass"),
            ("Payments:Provider", "mock"), ("Payments:Mock:Secret", "mock-dev-secret")), isProduction: true);
        problems.Should().Contain(p => p.StartsWith("Payments:Provider"));
        problems.Should().Contain(p => p.StartsWith("Payments:Mock:Secret"));
    }

    [Fact]
    public void Problem_messages_never_echo_secret_values()
    {
        const string leakyKey = "CHANGE_ME_DEV_ONLY_unique-marker-8f3a";
        var problems = HardeningGuard.Check(Cfg(SafePilot(
            ("Jwt:SigningKey", leakyKey),
            ("ConnectionStrings:Postgres", "Host=db;Username=zipzap;Password=zipzap"))), false);
        problems.Should().NotBeEmpty();
        string.Join("\n", problems).Should().NotContain("unique-marker-8f3a").And.NotContain("Password=zipzap");
    }
}
