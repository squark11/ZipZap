using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Npgsql;
using Xunit;
using ZipZap.Modules.Identity.Application;
using ZipZap.Modules.Identity.Infrastructure;

namespace ZipZap.Api.IntegrationTests;

/// <summary>Potwierdzenie e-mail na OSOBNEJ świeżej bazie, z przechwytywaniem poczty i wszystkich logów hosta.</summary>
public class VerifyApiFactory : ApiFactory
{
    private const string Database = "zipzap_it_verify";
    public CapturingEmailSender Mail { get; } = new();
    public CapturingLoggerProvider Logs { get; } = new();

    public VerifyApiFactory() => TestDatabases.Recreate(Database, owner: null);

    protected override string ConnectionString =>
        new NpgsqlConnectionStringBuilder(BaseConnectionString) { Database = Database }.ConnectionString;

    protected override void ConfigureSettings(IWebHostBuilder builder) =>
        builder.ConfigureTestServices(s =>
        {
            s.RemoveAll<IEmailSender>();
            s.AddSingleton<IEmailSender>(Mail);
            s.AddSingleton<ILoggerProvider>(Logs);
        });
}

[CollectionDefinition("verify")]
public sealed class VerifyCollection : ICollectionFixture<VerifyApiFactory> { }

[Collection("verify")]
public class EmailVerificationTests
{
    private static readonly Regex LinkToken = new(@"/verify-email#token=([0-9A-F]{64})\b", RegexOptions.Compiled);
    private const string VerifySubject = "Dowózka.pl — potwierdź adres e-mail";
    private readonly VerifyApiFactory _f;

    public EmailVerificationTests(VerifyApiFactory f) => _f = f;

    private async Task<List<EmailMessage>> MailsToAsync(string email, int expected, int timeoutMs = 5000)
    {
        var until = DateTime.UtcNow.AddMilliseconds(timeoutMs);
        List<EmailMessage> got;
        do
        {
            got = _f.Mail.Sent.Where(m => m.To == email && m.Subject == VerifySubject).ToList();
            if (got.Count >= expected) return got;
            await Task.Delay(50);
        } while (DateTime.UtcNow < until);
        return got;
    }

    /// <summary>Token z n-tej wiadomości potwierdzającej wysłanej na adres (poczta idzie przez kolejkę w tle).</summary>
    private async Task<string> TokenFromMailAsync(string email, int nth = 1)
    {
        var mails = await MailsToAsync(email, nth);
        mails.Should().HaveCountGreaterThanOrEqualTo(nth, "e-mail potwierdzający trafia do kolejki i jest wysyłany w tle");
        return LinkToken.Match(mails[nth - 1].Body).Groups[1].Value;
    }

    private async Task<(HttpStatusCode Status, string? Code, JsonElement Body)> PostAsync(string path, object body)
    {
        var resp = await _f.Anon().PostAsJsonAsync(path, body);
        var json = await resp.Content.ReadFromJsonAsync<JsonElement>();
        var code = json.TryGetProperty("title", out var t) ? t.GetString() : null;
        return (resp.StatusCode, code, json);
    }

    private Task<(HttpStatusCode Status, string? Code, JsonElement Body)> VerifyAsync(string token)
        => PostAsync("/api/identity/email/verify", new { token });

    private async Task<bool> IsVerifiedAsync(string userId)
    {
        await using var scope = _f.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        return await db.Users.AsNoTracking().Where(u => u.Id == Guid.Parse(userId)).Select(u => u.IsEmailVerified).SingleAsync();
    }

    private async Task ExpireVerificationLinksAsync(string userId)
    {
        await using var scope = _f.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE identity.user_tokens SET "ExpiresAtUtc" = now() - interval '1 minute'
            WHERE "UserId" = {Guid.Parse(userId)} AND "Type" = 'EmailVerification'
            """);
    }

    private async Task<AuthDto> RegisterStoreOwnerAsync()
    {
        var resp = await _f.Anon().PostAsJsonAsync("/api/register/store", new
        {
            email = $"store-{Guid.NewGuid():N}@test.pl", password = "Passw0rd!", fullName = "Właściciel Sklepu",
            storeName = $"Sklep {Guid.NewGuid():N}"[..16], city = "Kraków", nip = "1234563218",
        });
        resp.EnsureSuccessStatusCode();
        return (await resp.Content.ReadFromJsonAsync<AuthDto>())!;
    }

    [Theory]
    [InlineData("customer")]
    [InlineData("store-owner")]
    public async Task The_link_from_the_registration_mail_opens_the_panel_page_and_verifies_once(string kind)
    {
        var account = kind == "customer" ? await _f.RegisterCustomerAsync() : await RegisterStoreOwnerAsync();
        var email = account.user.email;

        var mail = (await MailsToAsync(email, 1)).Single();
        mail.Body.Should().Contain("http://localhost:4200/verify-email#token=")
            .And.NotContain("?token=", "token we fragmencie nie trafia do logów serwera WWW ani do Referer");
        var token = LinkToken.Match(mail.Body).Groups[1].Value;
        token.Should().NotBeEmpty();
        (await IsVerifiedAsync(account.user.id)).Should().BeFalse();

        var ok = await VerifyAsync(token);
        ok.Status.Should().Be(HttpStatusCode.OK);
        ok.Body.GetProperty("email").GetString().Should().MatchRegex(@"^.\*\*\*@test\.pl$").And.NotBe(email);
        (await IsVerifiedAsync(account.user.id)).Should().BeTrue();

        var again = await VerifyAsync(token);
        again.Status.Should().Be(HttpStatusCode.BadRequest);
        again.Code.Should().Be("validation.verify_token_used");

        AssertNeverLogged(token);
    }

    [Fact]
    public async Task Invalid_links_are_reported_distinctly_and_a_verification_token_is_not_a_reset_token()
    {
        foreach (var bad in new[] { "", "ABC", new string('A', 64), new string('A', 200) })
        {
            var r = await VerifyAsync(bad);
            r.Status.Should().Be(HttpStatusCode.BadRequest);
            r.Code.Should().Be("validation.verify_token_invalid");
        }

        var account = await _f.RegisterCustomerAsync();
        var token = await TokenFromMailAsync(account.user.email);
        (await PostAsync("/api/identity/password/reset/check", new { token })).Code
            .Should().Be("validation.reset_token_invalid", "tokeny różnych typów nie są wymienne");
    }

    [Fact]
    public async Task An_expired_link_is_reported_and_a_link_from_the_existing_resend_still_works()
    {
        var account = await _f.RegisterCustomerAsync();
        var email = account.user.email;
        var expired = await TokenFromMailAsync(email);
        await ExpireVerificationLinksAsync(account.user.id);

        var r = await VerifyAsync(expired);
        r.Status.Should().Be(HttpStatusCode.BadRequest);
        r.Code.Should().Be("validation.verify_token_expired");
        (await IsVerifiedAsync(account.user.id)).Should().BeFalse();

        (await _f.Authed(account.accessToken).PostAsJsonAsync("/api/identity/email/resend-verification", new { }))
            .EnsureSuccessStatusCode();
        var fresh = await TokenFromMailAsync(email, 2);
        fresh.Should().NotBe(expired);

        (await VerifyAsync(fresh)).Status.Should().Be(HttpStatusCode.OK);
        (await IsVerifiedAsync(account.user.id)).Should().BeTrue();
        (await VerifyAsync(expired)).Code.Should().Be("validation.verify_token_used", "adres jest już potwierdzony");

        AssertNeverLogged(expired);
        AssertNeverLogged(fresh);
    }

    [Fact]
    public async Task Verifying_disables_the_other_links_and_parallel_use_of_one_link_succeeds_once()
    {
        var account = await _f.RegisterCustomerAsync();
        var first = await TokenFromMailAsync(account.user.email);
        (await _f.Authed(account.accessToken).PostAsJsonAsync("/api/identity/email/resend-verification", new { }))
            .EnsureSuccessStatusCode();
        var second = await TokenFromMailAsync(account.user.email, 2);

        var results = await Task.WhenAll(Enumerable.Range(0, 5).Select(_ => VerifyAsync(second)));
        results.Count(r => r.Status == HttpStatusCode.OK).Should().Be(1, "ten sam link działa tylko raz, także równolegle");
        results.Where(r => r.Status != HttpStatusCode.OK).Should().OnlyContain(r => r.Code == "validation.verify_token_used");

        (await VerifyAsync(first)).Code.Should().Be("validation.verify_token_used", "po potwierdzeniu starsze linki nie są potrzebne");
    }

    [Fact]
    public async Task A_mail_provider_failure_does_not_break_registration_and_is_logged_without_the_link()
    {
        // Osobny host (ta sama baza, poczta i logi): jego kolejka czeka na ponowienie po awarii, a kolejka głównego
        // hosta — i poczta pozostałych testów kolekcji — płynie bez opóźnienia.
        using var host = _f.WithWebHostBuilder(_ => { });
        var email = $"it-{Guid.NewGuid():N}@test.pl";
        _f.Mail.Fail = true;
        try
        {
            var resp = await host.CreateClient().PostAsJsonAsync("/api/identity/register",
                new { email, password = "Passw0rd!", fullName = "IT User" });
            resp.StatusCode.Should().Be(HttpStatusCode.OK, "konto jest już zapisane — awaria poczty nie może zwrócić błędu rejestracji");

            var until = DateTime.UtcNow.AddSeconds(5);
            while (DateTime.UtcNow < until && !_f.Logs.Snapshot().Any(l => l.Message.Contains("[EMAIL:queue] wysyłka nieudana")))
                await Task.Delay(50);
        }
        finally { _f.Mail.Fail = false; }

        var failure = _f.Logs.Snapshot().Where(l => l.Message.Contains("[EMAIL:queue] wysyłka nieudana")).ToList();
        failure.Should().NotBeEmpty();
        failure.Should().OnlyContain(l => l.Message.Contains("InvalidOperationException") && l.Exception == null);
        AssertNeverLogged("#token=");
        _f.Logs.Snapshot().Should().NotContain(l => l.Message.Contains(email), "adres w logach jest maskowany");
    }

    private void AssertNeverLogged(string secret)
        => _f.Logs.Snapshot().Should().NotContain(
            l => l.Message.Contains(secret) || (l.Exception != null && l.Exception.Contains(secret)),
            "token potwierdzający nie może trafić do logów");
}
