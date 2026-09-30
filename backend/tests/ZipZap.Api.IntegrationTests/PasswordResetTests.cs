using System.Collections.Concurrent;
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

/// <summary>Poczta przechwytywana w teście; <see cref="Fail"/> symuluje awarię dostawcy (komunikat zawiera treść!).</summary>
public sealed class CapturingEmailSender : IEmailSender
{
    public ConcurrentQueue<EmailMessage> Sent { get; } = new();
    public volatile bool Fail;

    public Task SendAsync(EmailMessage message, CancellationToken ct = default)
    {
        if (Fail) throw new InvalidOperationException("dostawca poczty niedostępny: " + message.Body);
        Sent.Enqueue(message);
        return Task.CompletedTask;
    }
}

/// <summary>Reset hasła na OSOBNEJ świeżej bazie, z przechwytywaniem poczty i wszystkich logów hosta.</summary>
public class ResetApiFactory : ApiFactory
{
    private const string Database = "zipzap_it_reset";
    public CapturingEmailSender Mail { get; } = new();
    public CapturingLoggerProvider Logs { get; } = new();

    public ResetApiFactory() => TestDatabases.Recreate(Database, owner: null);

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

[CollectionDefinition("reset")]
public sealed class ResetCollection : ICollectionFixture<ResetApiFactory> { }

[Collection("reset")]
public class PasswordResetTests
{
    private static readonly Regex LinkToken = new(@"/reset-password#token=([0-9A-F]{64})\b", RegexOptions.Compiled);
    private const string ResetSubject = "Dowózka.pl — ustaw nowe hasło"; // rejestracja wysyła też e-mail weryfikacyjny
    private readonly ResetApiFactory _f;

    public PasswordResetTests(ResetApiFactory f) => _f = f;

    private async Task<(int Status, string Body)> ForgotAsync(string email)
    {
        var resp = await _f.Anon().PostAsJsonAsync("/api/identity/password/forgot", new { email });
        return ((int)resp.StatusCode, await resp.Content.ReadAsStringAsync());
    }

    private async Task<List<EmailMessage>> MailsToAsync(string email, int expected, int timeoutMs = 5000)
    {
        var until = DateTime.UtcNow.AddMilliseconds(timeoutMs);
        List<EmailMessage> got;
        do
        {
            got = _f.Mail.Sent.Where(m => m.To == email && m.Subject == ResetSubject).ToList();
            if (got.Count >= expected) return got;
            await Task.Delay(50);
        } while (DateTime.UtcNow < until);
        return got;
    }

    private async Task<string> RequestTokenAsync(string email, int nth = 1)
    {
        (await ForgotAsync(email)).Status.Should().Be(200);
        var mails = await MailsToAsync(email, nth);
        mails.Should().HaveCount(nth, "link resetu trafia do kolejki i jest wysyłany w tle");
        return LinkToken.Match(mails[nth - 1].Body).Groups[1].Value;
    }

    private async Task<(HttpStatusCode Status, string? Code, JsonElement Body)> PostAsync(string path, object body)
    {
        var resp = await _f.Anon().PostAsJsonAsync(path, body);
        var json = await resp.Content.ReadFromJsonAsync<JsonElement>();
        var code = json.TryGetProperty("title", out var t) ? t.GetString() : null;
        return (resp.StatusCode, code, json);
    }

    [Fact]
    public async Task Forgot_gives_the_same_answer_for_existing_and_unknown_accounts_and_mails_only_the_existing_one()
    {
        var existing = (await _f.RegisterCustomerAsync()).user.email;
        var unknown = $"nikt-{Guid.NewGuid():N}@test.pl";

        var a = await ForgotAsync(existing);
        var b = await ForgotAsync(unknown);

        a.Should().Be(b, "odpowiedź nie może zdradzać, czy konto istnieje");
        var mail = (await MailsToAsync(existing, 1)).Single();
        mail.Body.Should().Contain("http://localhost:4200/reset-password#token=")
            .And.NotContain("?token=", "token we fragmencie nie trafia do logów serwera WWW ani do Referer");
        LinkToken.IsMatch(mail.Body).Should().BeTrue();
        await Task.Delay(300);
        _f.Mail.Sent.Should().NotContain(m => m.To == unknown);
    }

    [Theory]
    [InlineData("customer")]
    [InlineData("StoreEmployee")]
    public async Task One_shared_reset_link_works_once_for_app_customers_and_panel_staff(string kind)
    {
        AuthDto account;
        if (kind == "customer") account = await _f.RegisterCustomerAsync();
        else
        {
            var admin = await _f.LoginAdminAsync();
            account = await _f.CreateStaffAsync(admin.accessToken, await _f.CreateStoreAsync(admin.accessToken), kind);
        }
        var email = account.user.email;
        var token = await RequestTokenAsync(email);

        var check = await PostAsync("/api/identity/password/reset/check", new { token });
        check.Status.Should().Be(HttpStatusCode.OK);
        check.Body.GetProperty("email").GetString().Should().MatchRegex(@"^.\*\*\*@test\.pl$").And.NotBe(email);

        (await PostAsync("/api/identity/password/reset", new { token, newPassword = "Nowe-Haslo-7" })).Status
            .Should().Be(HttpStatusCode.OK);

        (await _f.Anon().PostAsJsonAsync("/api/identity/login", new { email, password = "Passw0rd!" })).StatusCode
            .Should().Be(HttpStatusCode.Unauthorized);
        (await _f.LoginAsync(email, "Nowe-Haslo-7")).accessToken.Should().NotBeNullOrEmpty();
        (await _f.Anon().PostAsJsonAsync("/api/identity/refresh", new { refreshToken = account.refreshToken })).StatusCode
            .Should().Be(HttpStatusCode.Unauthorized, "zmiana hasła unieważnia wszystkie wcześniejsze sesje");

        (await PostAsync("/api/identity/password/reset/check", new { token })).Code.Should().Be("validation.reset_token_used");
        var again = await PostAsync("/api/identity/password/reset", new { token, newPassword = "Inne-Haslo-8" });
        again.Status.Should().Be(HttpStatusCode.BadRequest);
        again.Code.Should().Be("validation.reset_token_used");

        AssertNeverLogged(token);
    }

    [Fact]
    public async Task Invalid_and_expired_links_are_reported_distinctly()
    {
        (await PostAsync("/api/identity/password/reset/check", new { token = "ABC" })).Code
            .Should().Be("validation.reset_token_invalid");
        (await PostAsync("/api/identity/password/reset/check", new { token = new string('A', 64) })).Code
            .Should().Be("validation.reset_token_invalid");
        (await PostAsync("/api/identity/password/reset/check", new { token = "" })).Code
            .Should().Be("validation.reset_token_invalid");

        var account = await _f.RegisterCustomerAsync();
        var token = await RequestTokenAsync(account.user.email);
        await using (var scope = _f.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
            await db.Database.ExecuteSqlInterpolatedAsync($"""
                UPDATE identity.user_tokens SET "ExpiresAtUtc" = now() - interval '1 minute'
                WHERE "UserId" = {Guid.Parse(account.user.id)} AND "Type" = 'PasswordReset'
                """);
        }

        var check = await PostAsync("/api/identity/password/reset/check", new { token });
        check.Status.Should().Be(HttpStatusCode.BadRequest);
        check.Code.Should().Be("validation.reset_token_expired");
        (await PostAsync("/api/identity/password/reset", new { token, newPassword = "Nowe-Haslo-7" })).Code
            .Should().Be("validation.reset_token_expired");
        (await _f.LoginAsync(account.user.email, "Passw0rd!")).accessToken.Should().NotBeNullOrEmpty("hasło bez zmian");
    }

    [Fact]
    public async Task Using_one_link_disables_the_other_outstanding_links_and_parallel_use_succeeds_once()
    {
        var email = (await _f.RegisterCustomerAsync()).user.email;
        var first = await RequestTokenAsync(email, 1);
        var second = await RequestTokenAsync(email, 2);

        var results = await Task.WhenAll(Enumerable.Range(0, 5).Select(i =>
            PostAsync("/api/identity/password/reset", new { token = second, newPassword = $"Rownolegle-{i}-Haslo" })));
        results.Count(r => r.Status == HttpStatusCode.OK).Should().Be(1, "ten sam link działa tylko raz, także równolegle");
        results.Where(r => r.Status != HttpStatusCode.OK).Should().OnlyContain(r => r.Code == "validation.reset_token_used");

        (await PostAsync("/api/identity/password/reset/check", new { token = first })).Code
            .Should().Be("validation.reset_token_used", "po zmianie hasła starsze linki przestają działać");
    }

    [Fact]
    public async Task At_most_three_links_per_account_per_hour_with_an_unchanged_response()
    {
        var email = (await _f.RegisterCustomerAsync()).user.email;
        var answers = new List<(int, string)>();
        for (var i = 0; i < 5; i++) answers.Add(await ForgotAsync(email));

        answers.Distinct().Should().ContainSingle("limit na konto nie zmienia odpowiedzi");
        (await MailsToAsync(email, 3)).Should().HaveCount(3);
        await Task.Delay(300);
        _f.Mail.Sent.Count(m => m.To == email && m.Subject == ResetSubject).Should().Be(3);
    }

    [Fact]
    public async Task The_per_account_limit_holds_for_parallel_requests()
    {
        var email = (await _f.RegisterCustomerAsync()).user.email;
        var answers = await Task.WhenAll(Enumerable.Range(0, 10).Select(_ => ForgotAsync(email)));

        answers.Distinct().Should().ContainSingle();
        (await MailsToAsync(email, 3)).Should().HaveCount(3);
        await Task.Delay(500);
        _f.Mail.Sent.Count(m => m.To == email && m.Subject == ResetSubject)
            .Should().Be(3, "prośby są przetwarzane po kolei w tle, więc limit nie przecieka przy równoległych żądaniach");
    }

    [Fact]
    public async Task A_mail_provider_failure_is_invisible_to_the_caller_and_is_logged_without_the_link()
    {
        var email = (await _f.RegisterCustomerAsync()).user.email;
        var unknown = $"nikt-{Guid.NewGuid():N}@test.pl";
        _f.Mail.Fail = true;
        try
        {
            var a = await ForgotAsync(email);
            var b = await ForgotAsync(unknown);
            a.Should().Be(b);
            a.Status.Should().Be(200);

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

    [Fact]
    public async Task Only_the_hash_of_the_token_is_stored()
    {
        var account = await _f.RegisterCustomerAsync();
        var token = await RequestTokenAsync(account.user.email);
        await using var scope = _f.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        var hashes = await db.UserTokens.AsNoTracking()
            .Where(t => t.UserId == Guid.Parse(account.user.id)).Select(t => t.TokenHash).ToListAsync();
        hashes.Should().NotBeEmpty().And.NotContain(token);
    }

    [Fact]
    public async Task Public_config_says_mock_when_no_mail_channel_is_configured_and_live_otherwise()
    {
        var mock = await _f.Anon().GetFromJsonAsync<JsonElement>("/api/config/public");
        mock.GetProperty("emailDelivery").GetString().Should().Be("mock");

        using var withSmtp = _f.WithWebHostBuilder(b => b.UseSetting("Email:Smtp:Host", "127.0.0.1"));
        var live = await withSmtp.CreateClient().GetFromJsonAsync<JsonElement>("/api/config/public");
        live.GetProperty("emailDelivery").GetString().Should().Be("live");

        var admin = await _f.LoginAdminAsync();
        var status = await _f.Authed(admin.accessToken).GetFromJsonAsync<JsonElement>("/api/admin/config/status");
        status.GetProperty("emailChannel").GetString().Should().Be("none");
        status.GetProperty("email").GetBoolean().Should().BeFalse();
        status.GetProperty("identityPublicUrlProblem").ValueKind.Should().Be(JsonValueKind.Null, "lokalny dev dopuszcza http://localhost");
    }

    [Theory]
    [InlineData("https://panel.dowozka.pl", true, null)]
    [InlineData("https://panel.xn--dowzka-dxa.pl/", true, null)]
    [InlineData("http://panel.dowozka.pl", true, "HTTPS")]
    [InlineData("https://localhost:4200", true, "localhost")]
    [InlineData("http://localhost:4200", false, null)]
    [InlineData("https://panel.dowozka.pl/?x=1", true, "„?")]
    [InlineData("panel.dowozka.pl", true, "poprawnym")]
    public void Public_url_must_be_https_and_not_localhost_in_a_public_environment(string url, bool isPublic, string? problem)
    {
        var result = IdentityOptions.PublicUrlProblem(url, isPublic);
        if (problem is null) result.Should().BeNull();
        else result.Should().Contain(problem);
    }

    private void AssertNeverLogged(string secret)
        => _f.Logs.Snapshot().Should().NotContain(
            l => l.Message.Contains(secret) || (l.Exception != null && l.Exception.Contains(secret)),
            "token resetu nie może trafić do logów");
}
