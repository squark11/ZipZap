using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Xunit;
using ZipZap.Api.Configuration;
using ZipZap.Modules.Catalog.Domain;

namespace ZipZap.Api.IntegrationTests;

/// <summary>
/// S2 — wejście z kodu QR: adres aplikacji klienta (panel → env), licznik wejść na kartę sklepu wg źródła
/// (tylko agregat, bez danych osobowych; źródło to etykieta pomiaru, nie uprawnienie).
/// </summary>
[Collection("api")]
public sealed class StoreQrEntryTests
{
    private sealed record PublicConfig(string? customerAppUrl);
    private sealed record Source(string source, int count);
    private sealed record Stats(Guid storeId, int days, int total, List<Source> bySource);
    private sealed record StoreDto(Guid id, string slug);

    private readonly ApiFactory _f;
    public StoreQrEntryTests(ApiFactory f) => _f = f;

    [Theory]
    [InlineData("https://sklep.example.pl/", "https://sklep.example.pl", null)]
    [InlineData("  https://example.pl/app/  ", "https://example.pl/app", null)]
    [InlineData("http://localhost:4300", "http://localhost:4300", null)]
    [InlineData("http://sklep.example.pl", null, "HTTPS")]
    [InlineData("https://sklep.example.pl/?src=x", null, "parametrów")]
    [InlineData("https://sklep.example.pl/#/s", null, "parametrów")]
    [InlineData("sklep.example.pl", null, "pełnym adresem")]
    [InlineData("", null, null)]
    public void Customer_app_url_requires_https_without_query_or_fragment(string raw, string? expected, string? error)
    {
        var (value, err) = CustomerAppUrl.Normalize(raw);
        value.Should().Be(expected);
        if (error is null) err.Should().BeNull(); else err.Should().Contain(error);
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
            (await new CustomerAppUrlProvider(store, Cfg("https://env.example.pl/")).GetAsync())
                .Should().Be(new EffectiveCustomerAppUrl("https://env.example.pl", "env"));
            (await new CustomerAppUrlProvider(store, Cfg("http://env.example.pl")).GetAsync())
                .Should().Be(new EffectiveCustomerAppUrl(null, null), "nieprawidłowa wartość z env jest ignorowana");

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
                new { customerAppUrl = "https://sklep.example.pl/" })).EnsureSuccessStatusCode();
            (await _f.Anon().GetFromJsonAsync<PublicConfig>("/api/config/public"))!
                .customerAppUrl.Should().Be("https://sklep.example.pl");
        }
        finally
        {
            (await admin.PutAsJsonAsync("/api/admin/config/integrations", new { customerAppUrl = "" })).EnsureSuccessStatusCode();
        }
        (await _f.Anon().GetFromJsonAsync<PublicConfig>("/api/config/public"))!.customerAppUrl.Should().BeNull();
    }

    [Fact]
    public async Task Entries_are_counted_per_store_and_normalized_source_by_slug_or_id()
    {
        var admin = await _f.LoginAdminAsync();
        var store = await StoreAsync(admin.accessToken);

        (await PostEntryAsync(store.slug, "qr")).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await PostEntryAsync(store.id.ToString(), "QR")).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await PostEntryAsync(store.slug, "QR Kasa!!")).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await PostEntryAsync(store.slug, null)).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await PostEntryAsync(store.slug, new string('x', 200) + "<script>")).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await PostEntryAsync("nie-ma-takiego-sklepu", "qr")).StatusCode.Should().Be(HttpStatusCode.NotFound);

        var stats = await StatsAsync(admin.accessToken, store.id);
        stats.total.Should().Be(5);
        stats.bySource.Should().BeEquivalentTo(new[]
        {
            new Source("qr", 2), new Source("qr-kasa", 1), new Source(StoreEntrySource.Direct, 1),
            new Source(new string('x', StoreEntrySource.MaxLength), 1),
        }, "źródło to znormalizowana etykieta (a-z, 0-9, myślnik, maks. 40 znaków) — nic więcej nie trafia do bazy");
    }

    [Fact]
    public async Task Parallel_entries_are_all_counted()
    {
        var admin = await _f.LoginAdminAsync();
        var store = await StoreAsync(admin.accessToken);
        var responses = await OrderScenario.FireTogetherAsync(Enumerable.Range(0, 12)
            .Select(_ => (Func<Task<HttpResponseMessage>>)(() => PostEntryAsync(store.slug, "qr"))).ToList());
        responses.Should().OnlyContain(r => r.StatusCode == HttpStatusCode.NoContent);
        (await StatsAsync(admin.accessToken, store.id)).bySource.Should().ContainSingle(s => s.source == "qr" && s.count == 12);
    }

    [Fact]
    public async Task Entry_statistics_are_visible_only_to_the_store_staff_and_admin_and_the_source_grants_nothing()
    {
        var admin = await _f.LoginAdminAsync();
        var store = await StoreAsync(admin.accessToken);
        var own = await _f.CreateEmployeeAsync(admin.accessToken, store.id.ToString());
        var other = await _f.CreateEmployeeAsync(admin.accessToken, await _f.CreateStoreAsync(admin.accessToken));
        var customer = await _f.RegisterCustomerAsync();

        // Etykieta „admin"/„tester" w źródle to tylko tekst w liczniku — nie zmienia uprawnień.
        (await PostEntryAsync(store.slug, "admin")).StatusCode.Should().Be(HttpStatusCode.NoContent);
        var url = $"/api/catalog/stores/{store.id}/entries";
        (await _f.Anon().GetAsync(url)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await _f.Authed(customer.accessToken).GetAsync(url)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await _f.Authed(other.accessToken).GetAsync(url)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await _f.Authed(own.accessToken).GetAsync(url)).StatusCode.Should().Be(HttpStatusCode.OK);
        (await _f.Authed(customer.accessToken).GetAsync("/api/admin/config/status")).StatusCode
            .Should().Be(HttpStatusCode.Forbidden);
    }

    // ---------- Pomocnicze ----------

    private async Task<StoreDto> StoreAsync(string adminToken)
    {
        var id = await _f.CreateStoreAsync(adminToken);
        return (await _f.Anon().GetFromJsonAsync<StoreDto>($"/api/catalog/stores/{id}"))!;
    }

    private Task<HttpResponseMessage> PostEntryAsync(string idOrSlug, string? source)
        => _f.Anon().PostAsJsonAsync($"/api/catalog/stores/{idOrSlug}/entries", new { source });

    private async Task<Stats> StatsAsync(string token, Guid storeId)
        => (await _f.Authed(token).GetFromJsonAsync<Stats>($"/api/catalog/stores/{storeId}/entries?days=7"))!;

    private sealed class TestEnv(string root) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = "Development";
        public string ApplicationName { get; set; } = "zz-test";
        public string ContentRootPath { get; set; } = root;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
