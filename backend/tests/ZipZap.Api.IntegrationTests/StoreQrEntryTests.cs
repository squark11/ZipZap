using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Xunit;
using ZipZap.Api.Configuration;
using ZipZap.Modules.Catalog.Application;
using ZipZap.Modules.Catalog.Domain;
using ZipZap.Modules.Catalog.Infrastructure;

namespace ZipZap.Api.IntegrationTests;

/// <summary>
/// S2 — wejście z kodu QR: adres aplikacji klienta (panel → env, tylko katalog główny domeny, punycode), licznik wejść
/// na kartę sklepu (tylko agregat; anonimowy klient nie tworzy nowych źródeł — nieznane trafiają do „other"/„qr-other",
/// osobno liczone są wyłącznie etykiety QR zarejestrowane przez obsługę sklepu), retencja liczników dziennych.
/// Źródło to etykieta pomiaru, nie uprawnienie.
/// </summary>
[Collection("api")]
public sealed class StoreQrEntryTests
{
    private sealed record PublicConfig(string? customerAppUrl);
    private sealed record Source(string source, int count);
    private sealed record Stats(Guid storeId, int days, int total, List<Source> bySource, List<string> registeredQrSources);
    private sealed record StoreDto(Guid id, string slug);
    private sealed record Registered(List<string> registeredQrSources);

    private readonly ApiFactory _f;
    public StoreQrEntryTests(ApiFactory f) => _f = f;

    // ---------- Adres aplikacji klienta ----------

    [Theory]
    [InlineData("https://sklep.example.pl/", "https://sklep.example.pl", null)]
    [InlineData("https://app.dowózka.pl", "https://app.xn--dowzka-dxa.pl", null)]
    [InlineData("http://localhost:4300", "http://localhost:4300", null)]
    [InlineData("https://example.pl/app/", null, "katalogiem głównym")]
    [InlineData("https://dowózka.pl/app", null, "katalogiem głównym")]
    [InlineData("http://sklep.example.pl", null, "HTTPS")]
    [InlineData("https://sklep.example.pl/?src=x", null, "parametrów")]
    [InlineData("https://sklep.example.pl/#/s", null, "parametrów")]
    [InlineData("sklep.example.pl", null, "pełnym adresem")]
    [InlineData("", null, null)]
    public void Customer_app_url_is_the_https_root_of_a_host_in_punycode(string raw, string? expected, string? error)
    {
        var (value, err) = CustomerAppUrl.Normalize(raw);
        value.Should().Be(expected);
        if (error is null) err.Should().BeNull(); else err.Should().Contain(error);
    }

    [Fact]
    public void Cors_allowlist_also_accepts_the_punycode_origin_of_an_idn_host()
    {
        CorsOrigins.WithPunycode(new[] { "https://app.dowózka.pl/", "https://panel.example.pl" }).Should().BeEquivalentTo(
            "https://app.dowózka.pl", "https://app.xn--dowzka-dxa.pl", "https://panel.example.pl");
    }

    [Fact]
    public async Task Customer_app_url_comes_from_the_panel_and_falls_back_to_configuration()
    {
        var root = Directory.CreateTempSubdirectory("zz-appurl-").FullName;
        try
        {
            var store = new PlatformIntegrationsStore(new TestEnv(root), DataProtectionProvider.Create("zz-test"));
            IConfiguration Cfg(string? v) => new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?> { [CustomerAppUrlProvider.ConfigKey] = v }).Build();

            (await new CustomerAppUrlProvider(store, Cfg(null)).GetAsync()).Should().Be(new EffectiveCustomerAppUrl(null, null));
            (await new CustomerAppUrlProvider(store, Cfg("https://app.dowózka.pl/")).GetAsync())
                .Should().Be(new EffectiveCustomerAppUrl("https://app.xn--dowzka-dxa.pl", "env"));
            (await new CustomerAppUrlProvider(store, Cfg("https://dowózka.pl/app")).GetAsync())
                .Should().Be(new EffectiveCustomerAppUrl(null, null), "adres z podścieżką z env jest ignorowany");

            await store.SaveAsync(new PlatformIntegrationsUpdate(null, null, null, null, CustomerAppUrl: "https://panel.example.pl"));
            (await new CustomerAppUrlProvider(store, Cfg("https://env.example.pl")).GetAsync())
                .Should().Be(new EffectiveCustomerAppUrl("https://panel.example.pl", "panel"), "panel ma pierwszeństwo");
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    [Fact]
    public async Task Admin_sets_the_customer_app_url_which_is_public_and_invalid_values_are_rejected()
    {
        var admin = _f.Authed((await _f.LoginAdminAsync()).accessToken);
        try
        {
            (await admin.PutAsJsonAsync("/api/admin/config/integrations",
                new { customerAppUrl = "http://sklep.example.pl" })).StatusCode.Should().Be(HttpStatusCode.BadRequest);
            (await admin.PutAsJsonAsync("/api/admin/config/integrations",
                new { customerAppUrl = "https://dowózka.pl/app" })).StatusCode.Should().Be(HttpStatusCode.BadRequest);

            (await admin.PutAsJsonAsync("/api/admin/config/integrations",
                new { customerAppUrl = "https://app.dowózka.pl/" })).EnsureSuccessStatusCode();
            (await _f.Anon().GetFromJsonAsync<PublicConfig>("/api/config/public"))!
                .customerAppUrl.Should().Be("https://app.xn--dowzka-dxa.pl");
        }
        finally
        {
            (await admin.PutAsJsonAsync("/api/admin/config/integrations", new { customerAppUrl = "" })).EnsureSuccessStatusCode();
        }
        (await _f.Anon().GetFromJsonAsync<PublicConfig>("/api/config/public"))!.customerAppUrl.Should().BeNull();
    }

    // ---------- Licznik wejść: ograniczona lista źródeł ----------

    [Fact]
    public async Task Unknown_sources_are_aggregated_and_only_registered_qr_labels_are_counted_separately()
    {
        var admin = await _f.LoginAdminAsync();
        var store = await StoreAsync(admin.accessToken);

        (await PostEntryAsync(store.slug, "qr")).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await PostEntryAsync(store.id.ToString(), "QR")).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await PostEntryAsync(store.slug, "landing")).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await PostEntryAsync(store.slug, "QR Kasa!!")).StatusCode.Should().Be(HttpStatusCode.NoContent); // niezarejestrowana
        (await PostEntryAsync(store.slug, null)).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await PostEntryAsync(store.slug, "promo-" + new string('x', 200) + "<script>")).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await PostEntryAsync("nie-ma-takiego-sklepu", "qr")).StatusCode.Should().Be(HttpStatusCode.NotFound);

        (await RegisterAsync(admin.accessToken, store.id, "qr-kasa")).StatusCode.Should().Be(HttpStatusCode.OK);
        (await PostEntryAsync(store.slug, "qr-kasa")).StatusCode.Should().Be(HttpStatusCode.NoContent);

        var stats = await StatsAsync(admin.accessToken, store.id);
        stats.total.Should().Be(7);
        stats.bySource.Should().BeEquivalentTo(new[]
        {
            new Source(StoreEntrySource.Qr, 2), new Source(StoreEntrySource.Landing, 1), new Source(StoreEntrySource.QrOther, 1),
            new Source(StoreEntrySource.Direct, 1), new Source(StoreEntrySource.Other, 1), new Source("qr-kasa", 1),
        });
        stats.registeredQrSources.Should().Equal("qr-kasa");
    }

    [Fact]
    public async Task Many_different_labels_from_an_anonymous_client_do_not_create_new_rows()
    {
        var admin = await _f.LoginAdminAsync();
        var store = await StoreAsync(admin.accessToken);
        for (var i = 0; i < 40; i++)
        {
            (await PostEntryAsync(store.slug, $"qr-atak-{i}")).EnsureSuccessStatusCode();
            (await PostEntryAsync(store.slug, $"kampania-{i}")).EnsureSuccessStatusCode();
        }

        (await RowsAsync(store.id)).Should().BeEquivalentTo(new[] { StoreEntrySource.QrOther, StoreEntrySource.Other },
            "80 różnych etykiet = 2 wiersze (qr-other i other), nie 80");
        (await StatsAsync(admin.accessToken, store.id)).total.Should().Be(80, "wejścia nadal są liczone");
    }

    [Fact]
    public async Task Parallel_entries_are_all_counted_and_cannot_create_sources_beyond_the_list()
    {
        var admin = await _f.LoginAdminAsync();
        var store = await StoreAsync(admin.accessToken);
        var responses = await OrderScenario.FireTogetherAsync(Enumerable.Range(0, 36)
            .Select(i => (Func<Task<HttpResponseMessage>>)(() =>
                PostEntryAsync(store.slug, (i % 3) switch { 0 => "qr", 1 => $"qr-wyscig-{i}", _ => $"inne-{i}" })))
            .ToList());
        responses.Should().OnlyContain(r => r.StatusCode == HttpStatusCode.NoContent);

        (await StatsAsync(admin.accessToken, store.id)).bySource.Should().BeEquivalentTo(new[]
        {
            new Source(StoreEntrySource.Qr, 12), new Source(StoreEntrySource.QrOther, 12), new Source(StoreEntrySource.Other, 12),
        });
    }

    // ---------- Rejestracja etykiet QR ----------

    [Fact]
    public async Task Only_store_staff_can_register_labels_and_invalid_or_reserved_labels_are_rejected()
    {
        var admin = await _f.LoginAdminAsync();
        var store = await StoreAsync(admin.accessToken);
        var own = await _f.CreateEmployeeAsync(admin.accessToken, store.id.ToString());
        var other = await _f.CreateEmployeeAsync(admin.accessToken, await _f.CreateStoreAsync(admin.accessToken));
        var customer = await _f.RegisterCustomerAsync();

        (await _f.Anon().PostAsJsonAsync($"/api/catalog/stores/{store.id}/qr-sources", new { source = "qr-kasa" }))
            .StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await RegisterAsync(customer.accessToken, store.id, "qr-kasa")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await RegisterAsync(other.accessToken, store.id, "qr-kasa")).StatusCode.Should().Be(HttpStatusCode.Forbidden);

        foreach (var bad in new[] { "kasa", "qr-", "qr-other", "qr-ka sa", "qr-kasa!", "qr-" + new string('a', 40) })
            (await RegisterAsync(own.accessToken, store.id, bad)).StatusCode.Should().Be(HttpStatusCode.BadRequest, bad);

        var ok = await RegisterAsync(own.accessToken, store.id, "qr-kasa");
        ok.StatusCode.Should().Be(HttpStatusCode.OK);
        (await ok.Content.ReadFromJsonAsync<Registered>())!.registeredQrSources.Should().Equal("qr-kasa");
        (await RegisterAsync(own.accessToken, store.id, "qr-kasa")).StatusCode.Should().Be(HttpStatusCode.OK, "ponowna rejestracja — bez zmian");
    }

    [Fact]
    public async Task At_most_twenty_labels_per_store_even_with_parallel_registrations()
    {
        var admin = await _f.LoginAdminAsync();
        var store = await StoreAsync(admin.accessToken);

        var responses = await OrderScenario.FireTogetherAsync(Enumerable.Range(0, 30)
            .Select(i => (Func<Task<HttpResponseMessage>>)(() => RegisterAsync(admin.accessToken, store.id, $"qr-miejsce-{i}")))
            .ToList());

        responses.Count(r => r.StatusCode == HttpStatusCode.OK).Should().Be(StoreEntrySource.MaxRegisteredQrSources);
        responses.Count(r => r.StatusCode == HttpStatusCode.Conflict).Should().Be(30 - StoreEntrySource.MaxRegisteredQrSources);
        using var scope = _f.Services.CreateScope();
        (await scope.ServiceProvider.GetRequiredService<CatalogDbContext>().StoreQrSources.CountAsync(q => q.StoreId == store.id))
            .Should().Be(StoreEntrySource.MaxRegisteredQrSources, "blokada wiersza sklepu — wyścig nie przekracza limitu");

        var existing = (await StatsAsync(admin.accessToken, store.id)).registeredQrSources[0];
        (await RegisterAsync(admin.accessToken, store.id, existing)).StatusCode.Should().Be(HttpStatusCode.OK,
            "istniejąca etykieta działa także przy pełnym limicie");
    }

    // ---------- Retencja ----------

    [Fact]
    public async Task Daily_counters_older_than_the_retention_period_are_rolled_into_monthly_totals_once()
    {
        var admin = await _f.LoginAdminAsync();
        var store = await StoreAsync(admin.accessToken);
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var keepFrom = today.AddDays(-StoreEntrySource.DailyRetentionDays);
        var oldMonth = new DateOnly(keepFrom.Year, keepFrom.Month, 1).AddMonths(-1);

        using (var scope = _f.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<CatalogDbContext>();
            foreach (var (day, source, count) in new[]
                     { (oldMonth, "qr", 3), (oldMonth.AddDays(5), "qr", 4), (oldMonth.AddDays(6), "other", 2), (today, "qr", 7) })
                await db.Database.ExecuteSqlInterpolatedAsync(
                    $"""INSERT INTO catalog.store_entry_stats ("StoreId","Day","Source","Count") VALUES ({store.id}, {day}, {source}, {count})""");
        }

        await CompactAsync(keepFrom);
        await CompactAsync(keepFrom); // drugie uruchomienie (np. druga instancja) niczego nie dubluje

        using (var scope = _f.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<CatalogDbContext>();
            (await db.StoreEntryStats.AsNoTracking().Where(s => s.StoreId == store.id).Select(s => s.Day).ToListAsync())
                .Should().Equal(today);
            (await db.StoreEntryMonthlyStats.AsNoTracking().Where(s => s.StoreId == store.id)
                    .Select(s => new { s.Month, s.Source, s.Count }).ToListAsync())
                .Should().BeEquivalentTo(new[] { new { Month = oldMonth, Source = "qr", Count = 7 }, new { Month = oldMonth, Source = "other", Count = 2 } });
        }
        (await StatsAsync(admin.accessToken, store.id, days: 365)).days.Should().Be(StoreEntrySource.DailyRetentionDays);
    }

    [Fact]
    public async Task Entry_statistics_are_visible_only_to_the_store_staff_and_admin_and_the_source_grants_nothing()
    {
        var admin = await _f.LoginAdminAsync();
        var store = await StoreAsync(admin.accessToken);
        var own = await _f.CreateEmployeeAsync(admin.accessToken, store.id.ToString());
        var other = await _f.CreateEmployeeAsync(admin.accessToken, await _f.CreateStoreAsync(admin.accessToken));
        var customer = await _f.RegisterCustomerAsync();

        // „admin"/„tester" w źródle to tylko tekst — trafia do „other" i nie zmienia uprawnień ani ról.
        (await PostEntryAsync(store.slug, "admin")).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await PostEntryAsync(store.slug, "tester")).StatusCode.Should().Be(HttpStatusCode.NoContent);
        var url = $"/api/catalog/stores/{store.id}/entries";
        (await _f.Anon().GetAsync(url)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await _f.Authed(customer.accessToken).GetAsync(url)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await _f.Authed(other.accessToken).GetAsync(url)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await _f.Authed(own.accessToken).GetAsync(url)).StatusCode.Should().Be(HttpStatusCode.OK);
        (await _f.Authed(customer.accessToken).GetAsync("/api/admin/config/status")).StatusCode
            .Should().Be(HttpStatusCode.Forbidden);
        (await _f.LoginAsync(customer.user.email, "Passw0rd!")).user.roles.Should().Equal(customer.user.roles);
        (await StatsAsync(admin.accessToken, store.id)).bySource.Should().ContainSingle(s => s.source == StoreEntrySource.Other && s.count == 2);
    }

    // ---------- Pomocnicze ----------

    private async Task<StoreDto> StoreAsync(string adminToken)
    {
        var id = await _f.CreateStoreAsync(adminToken);
        return (await _f.Anon().GetFromJsonAsync<StoreDto>($"/api/catalog/stores/{id}"))!;
    }

    private Task<HttpResponseMessage> PostEntryAsync(string idOrSlug, string? source)
        => _f.Anon().PostAsJsonAsync($"/api/catalog/stores/{idOrSlug}/entries", new { source });

    private Task<HttpResponseMessage> RegisterAsync(string token, Guid storeId, string source)
        => _f.Authed(token).PostAsJsonAsync($"/api/catalog/stores/{storeId}/qr-sources", new { source });

    private async Task<Stats> StatsAsync(string token, Guid storeId, int days = 7)
        => (await _f.Authed(token).GetFromJsonAsync<Stats>($"/api/catalog/stores/{storeId}/entries?days={days}"))!;

    private async Task<List<string>> RowsAsync(Guid storeId)
    {
        using var scope = _f.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<CatalogDbContext>().StoreEntryStats.AsNoTracking()
            .Where(s => s.StoreId == storeId).Select(s => s.Source).ToListAsync();
    }

    private async Task CompactAsync(DateOnly keepFrom)
    {
        using var scope = _f.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<CatalogService>().CompactEntryStatsAsync(keepFrom, CancellationToken.None);
    }

    private sealed class TestEnv(string root) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = "Development";
        public string ApplicationName { get; set; } = "zz-test";
        public string ContentRootPath { get; set; } = root;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
