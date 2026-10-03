using System.Collections.Concurrent;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using Xunit;
using ZipZap.Api.Configuration;

namespace ZipZap.Api.IntegrationTests;

/// <summary>Konfiguracja platformy na OSOBNEJ świeżej bazie — testy zmieniają ustawienia globalne (SMTP, opłaty).</summary>
public class ConfigApiFactory : ApiFactory
{
    private const string Database = "zipzap_it_config";

    public ConfigApiFactory() => TestDatabases.Recreate(Database, owner: null);

    protected override string ConnectionString =>
        new NpgsqlConnectionStringBuilder(BaseConnectionString) { Database = Database }.ConnectionString;

    /// <summary>
    /// „Druga instancja API" dla warstwy konfiguracji: osobny kontener DI (bez pamięci podręcznej pierwszej instancji)
    /// na tej samej bazie — jak nowy kontener po wdrożeniu. Lżejsza niż pełny host (bez usług w tle), bo kolekcje testów
    /// dzielą limit połączeń jednego Postgresa.
    /// </summary>
    public ServiceProvider NewConfigInstance()
    {
        var s = new ServiceCollection();
        s.AddLogging();
        s.AddDbContext<PlatformConfigDbContext>(o => o.UseNpgsql(ConnectionString));
        s.AddSingleton<IConfigDocuments, PostgresConfigDocuments>();
        s.AddDataProtection().SetApplicationName("ZipZap").PersistKeysToDbContext<PlatformConfigDbContext>();
        s.AddSingleton<PlatformIntegrationsStore>();
        s.AddSingleton<StoreIntegrationStore>();
        s.AddSingleton<StoreLegalStore>();
        return s.BuildServiceProvider();
    }
}

[CollectionDefinition("config")]
public sealed class ConfigCollection : ICollectionFixture<ConfigApiFactory> { }

/// <summary>Dokumenty konfiguracji w pamięci — do testów magazynów bez bazy.</summary>
internal sealed class InMemoryConfigDocuments : IConfigDocuments
{
    private readonly ConcurrentDictionary<string, string> _docs = new();
    private readonly object _gate = new();

    public Task<T?> GetAsync<T>(string key, CancellationToken ct = default) where T : class
        => Task.FromResult(_docs.TryGetValue(key, out var json) ? JsonSerializer.Deserialize<T>(json) : null);

    public Task<T> UpdateAsync<T>(string key, Func<T?, T> update, CancellationToken ct = default) where T : class
    {
        lock (_gate)
        {
            var next = update(_docs.TryGetValue(key, out var json) ? JsonSerializer.Deserialize<T>(json) : null);
            _docs[key] = JsonSerializer.Serialize(next);
            return Task.FromResult(next);
        }
    }
}

/// <summary>
/// Konfiguracja edytowana w panelu i klucze Data Protection leżą w bazie: przeżywają nowy host (wdrożenie na hostingu
/// bez trwałego dysku), sekrety zapisane przez jedną instancję odszyfrowuje druga, równoległe zapisy nie giną, a dawne
/// pliki App_Data są przenoszone raz i nigdy nie nadpisują bazy.
/// </summary>
[Collection("config")]
public sealed class PlatformConfigPersistenceTests
{
    private sealed record Counter(int N);

    private readonly ConfigApiFactory _f;
    public PlatformConfigPersistenceTests(ConfigApiFactory f) => _f = f;

    [Fact]
    public async Task Panel_settings_and_encrypted_secrets_survive_a_new_host()
    {
        var admin = _f.Authed((await _f.LoginAdminAsync()).accessToken);
        var storeId = Guid.NewGuid();
        (await admin.PutAsJsonAsync($"/api/stores/{storeId}/legal",
            new { termsUrl = "https://sklep.pl/regulamin", privacyUrl = "https://sklep.pl/prywatnosc", requiresAcceptance = true }))
            .EnsureSuccessStatusCode();
        (await admin.PutAsJsonAsync($"/api/payments/stores/{storeId}/integration",
            new { provider = "przelewy24", merchantId = "12345", sandbox = true, apiKey = "klucz-api-sklepu" }))
            .EnsureSuccessStatusCode();
        (await admin.PutAsJsonAsync("/api/admin/config/integrations", new
        {
            smtpHost = "smtp.example.pl", smtpPort = 465, smtpUsername = "nadawca@example.pl",
            smtpPassword = "haslo-smtp-7", smtpFromEmail = "nadawca@example.pl",
        })).EnsureSuccessStatusCode();

        // Nowa instancja = nowy kontener na Renderze: nic z pamięci ani z dysku poprzedniej.
        await using var next = _f.NewConfigInstance();
        var legal = await next.GetRequiredService<StoreLegalStore>().GetAsync(storeId);
        (legal.TermsUrl, legal.RequiresAcceptance).Should().Be(("https://sklep.pl/regulamin", true));
        var payment = await next.GetRequiredService<StoreIntegrationStore>().GetStatusAsync(storeId);
        (payment.Provider, payment.MerchantId, payment.HasApiKey).Should().Be(("przelewy24", "12345", true));
        var smtp = await next.GetRequiredService<PlatformIntegrationsStore>().GetSmtpAsync();
        smtp.Host.Should().Be("smtp.example.pl");
        smtp.Password.Should().Be("haslo-smtp-7", "klucze Data Protection są w bazie, więc druga instancja odszyfrowuje sekret");

        await using var scope = _f.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformConfigDbContext>();
        (await db.DataProtectionKeys.CountAsync()).Should().BeGreaterThan(0);
        (await db.Documents.Where(d => d.Key == StoreIntegrationStore.KeyPrefix + storeId).Select(d => d.Json).SingleAsync())
            .Should().NotContain("klucz-api-sklepu", "sekret jest w bazie wyłącznie zaszyfrowany");
    }

    [Fact]
    public async Task Parallel_updates_of_one_document_are_never_lost()
    {
        var docs = _f.Services.GetRequiredService<IConfigDocuments>();
        var key = $"test:counter:{Guid.NewGuid():N}";

        // Umiarkowana równoległość: każdy czekający zapis trzyma połączenie, a kolekcje testów dzielą limit
        // połączeń jednego Postgresa (100) — więcej nie dowodzi niczego ponad to.
        await Task.WhenAll(Enumerable.Range(0, 8).Select(_ =>
            docs.UpdateAsync<Counter>(key, c => new Counter((c?.N ?? 0) + 1))));

        (await docs.GetAsync<Counter>(key))!.N.Should().Be(8);
    }

    [Fact]
    public async Task An_unreadable_document_fails_loudly_instead_of_reading_as_empty_settings()
    {
        var key = $"test:broken:{Guid.NewGuid():N}";
        await using (var scope = _f.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PlatformConfigDbContext>();
            db.Documents.Add(new ConfigDocument(key, "[1, 2]", DateTime.UtcNow));
            await db.SaveChangesAsync();
        }

        var read = () => _f.Services.GetRequiredService<IConfigDocuments>().GetAsync<Counter>(key);
        await read.Should().ThrowAsync<JsonException>("brak cichego pustego ustawienia (np. wyłączonej captchy)");
    }

    [Fact]
    public async Task Legacy_App_Data_files_are_imported_once_and_never_overwrite_the_database()
    {
        var dir = Directory.CreateTempSubdirectory("zz-appdata-").FullName;
        try
        {
            var storeId = Guid.NewGuid();
            var settings = _f.Services.GetRequiredService<PlatformSettingsStore>();
            await settings.SaveAsync(new PlatformSettings { ZipZapDeliveryFee = 42m });

            File.WriteAllText(Path.Combine(dir, "platform-settings.json"), """{ "ZipZapDeliveryFee": 19 }""");
            File.WriteAllText(Path.Combine(dir, "store-legal.json"),
                $$"""{ "{{storeId}}": { "TermsUrl": "https://stary.pl/regulamin", "PrivacyUrl": "https://stary.pl/rodo", "RequiresAcceptance": true } }""");
            File.WriteAllText(Path.Combine(dir, "store-billing.json"), $$"""{ "{{storeId}}": { "Plan": "B" } }""");
            File.WriteAllText(Path.Combine(dir, "store-integrations.json"), "to nie jest JSON");
            // Klucz z dawnego repozytorium plikowego (ta sama nazwa aplikacji co API) i sekret nim zaszyfrowany.
            var legacy = DataProtectionProvider.Create(new DirectoryInfo(Path.Combine(dir, "keys")), b => b.SetApplicationName("ZipZap"));
            var payload = legacy.CreateProtector("zz-legacy").Protect("stary-sekret");

            var imported = await LegacyAppDataImport.RunAsync(_f.Services, dir, NullLogger.Instance);
            imported.Should().Be(3, "dokumenty sklepu (prawny, rozliczenia) i klucz; ustawienia platformy już są w bazie");
            (await LegacyAppDataImport.RunAsync(_f.Services, dir, NullLogger.Instance)).Should().Be(0, "import jest jednorazowy");

            (await settings.GetAsync()).ZipZapDeliveryFee.Should().Be(42m, "wpis z bazy wygrywa z dawnym plikiem");
            var legal = await _f.Services.GetRequiredService<StoreLegalStore>().GetAsync(storeId);
            legal.TermsUrl.Should().Be("https://stary.pl/regulamin");
            legal.RequiresAcceptance.Should().BeTrue();
            (await _f.Services.GetRequiredService<StoreBillingStore>().GetAsync(storeId)).Plan.Should().Be("B");

            await using var next = _f.NewConfigInstance();
            next.GetRequiredService<IDataProtectionProvider>().CreateProtector("zz-legacy").Unprotect(payload)
                .Should().Be("stary-sekret", "zaszyfrowane wcześniej sekrety dalej dają się odczytać");
        }
        finally { Directory.Delete(dir, recursive: true); }
    }
}
