using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Npgsql;
using Xunit;

namespace ZipZap.Api.IntegrationTests;

public sealed record AuthUser(string id, string email, string[] roles, string[] storeIds);
public sealed record AuthDto(string accessToken, string refreshToken, AuthUser user);

/// <summary>
/// Bootuje całe API (WebApplicationFactory) na osobnej bazie testowej. Wymaga
/// działającego Postgresa (Docker) na localhost:5432 — baza `zipzap_it` jest
/// tworzona i migrowana przy pierwszym starcie.
///
/// Limiter nadużyć jest WŁĄCZONY (jak na produkcji). Każdy klient z <see cref="Anon"/>/<see cref="Authed"/>
/// symuluje osobnego użytkownika za proxy hostingu (własny X-Forwarded-For), więc testy funkcjonalne
/// nie dzielą jednego „kubełka" limitu. Testy limitera używają <see cref="ClientFromIp"/>.
/// </summary>
public class ApiFactory : WebApplicationFactory<Program>
{
    private static int _ipCounter;

    public static string BaseConnectionString =>
        Environment.GetEnvironmentVariable("ZIPZAP_TEST_POSTGRES")
        ?? "Host=localhost;Port=5432;Database=zipzap_it;Username=zipzap;Password=zipzap";

    protected virtual string ConnectionString => BaseConnectionString;

    /// <summary>Dodatkowe ustawienia wariantu (np. tryb publicznego pilotażu).</summary>
    protected virtual void ConfigureSettings(IWebHostBuilder builder) { }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development"); // tryb hartowany włącza Pilot:Public, nie nazwa środowiska
        builder.UseSetting("ConnectionStrings:Postgres", ConnectionString);
        ConfigureSettings(builder);
    }

    private static string NextClientIp()
    {
        var n = Interlocked.Increment(ref _ipCounter);
        return $"10.{(n >> 16) & 255}.{(n >> 8) & 255}.{n & 255}";
    }

    /// <summary>Klient z jawnie podanym X-Forwarded-For (testy limitera / podrabiania nagłówka).</summary>
    public HttpClient ClientFromIp(string forwardedFor, string? accessToken = null)
    {
        var c = CreateClient();
        c.DefaultRequestHeaders.TryAddWithoutValidation("X-Forwarded-For", forwardedFor);
        if (accessToken is not null)
            c.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        return c;
    }

    public HttpClient Anon() => ClientFromIp(NextClientIp());

    public HttpClient Authed(string accessToken) => ClientFromIp(NextClientIp(), accessToken);

    public async Task<AuthDto> RegisterCustomerAsync(string? email = null)
    {
        email ??= $"it-{Guid.NewGuid():N}@test.pl";
        var resp = await Anon().PostAsJsonAsync("/api/identity/register",
            new { email, password = "Passw0rd!", fullName = "IT User" });
        resp.EnsureSuccessStatusCode();
        return (await resp.Content.ReadFromJsonAsync<AuthDto>())!;
    }

    public async Task<AuthDto> LoginAsync(string email, string password)
    {
        var resp = await Anon().PostAsJsonAsync("/api/identity/login", new { email, password });
        resp.EnsureSuccessStatusCode();
        return (await resp.Content.ReadFromJsonAsync<AuthDto>())!;
    }

    public Task<AuthDto> LoginAdminAsync() => LoginAsync("admin@zipzap.local", "Admin123!");

    /// <summary>Admin tworzy sklep i zwraca jego Id.</summary>
    public async Task<string> CreateStoreAsync(string adminToken, string? name = null)
    {
        name ??= $"IT Store {Guid.NewGuid():N}".Substring(0, 20);
        var resp = await Authed(adminToken).PostAsJsonAsync("/api/catalog/stores", new
        {
            name,
            city = "Testowo",
            commissionRate = 0.10m,
            minimumOrderValue = 0m,
        });
        resp.EnsureSuccessStatusCode();
        var doc = await resp.Content.ReadFromJsonAsync<Dictionary<string, object>>();
        return doc!["id"].ToString()!;
    }

    /// <summary>Admin tworzy pracownika przypisanego do sklepu i loguje go.</summary>
    public Task<AuthDto> CreateEmployeeAsync(string adminToken, string storeId)
        => CreateStaffAsync(adminToken, storeId, "StoreEmployee");

    /// <summary>Admin tworzy aktywnego użytkownika z rolą przypisaną do sklepu (StoreEmployee/Driver) i loguje go.</summary>
    public async Task<AuthDto> CreateStaffAsync(string adminToken, string storeId, string role)
    {
        var email = $"{role.ToLowerInvariant()}-{Guid.NewGuid():N}@test.pl";
        var resp = await Authed(adminToken).PostAsJsonAsync("/api/identity/admin/users", new
        {
            email,
            password = "Passw0rd!",
            fullName = $"IT {role}",
            role,
            storeId,
        });
        resp.EnsureSuccessStatusCode();
        return await LoginAsync(email, "Passw0rd!");
    }
}

/// <summary>
/// Wariant „publiczny pilotaż" (Pilot:Public=true) na OSOBNEJ, świeżo tworzonej bazie.
/// Baza jest dostępna przez rolę z niedomyślnym hasłem, więc guard fail-fast przechodzi
/// uczciwie (bez obchodzenia reguły o domyślnym haśle bazy).
/// </summary>
public class PilotApiFactory : ApiFactory
{
    public const string AllowedOrigin = "https://panel.pilot-it.example";
    private const string PilotRole = "zipzap_pilot_it";
    private const string PilotPassword = "Pilot-IT-Str0ng-Passw0rd!";

    private readonly string _database;
    private readonly Action<IWebHostBuilder>? _extra;

    // xUnit wymaga dokładnie jednego PUBLICZNEGO konstruktora dla fixture'a kolekcji;
    // warianty (inna baza / dodatkowe ustawienia) tworzą testy przez konstruktor internal.
    public PilotApiFactory() : this("zipzap_it_pilot") { }

    internal PilotApiFactory(string database, Action<IWebHostBuilder>? extra = null, bool recreateDatabase = true)
    {
        _database = database;
        _extra = extra;
        if (recreateDatabase) RecreateDatabase(database);
    }

    protected override string ConnectionString => new NpgsqlConnectionStringBuilder(BaseConnectionString)
    {
        Database = _database, Username = PilotRole, Password = PilotPassword,
    }.ConnectionString;

    protected override void ConfigureSettings(IWebHostBuilder builder)
    {
        builder.UseSetting("Pilot:Public", "true");
        builder.UseSetting("Pilot:AdminPasswordConfirmed", "true");
        builder.UseSetting("Jwt:SigningKey", "pilot-it-signing-key-" + new string('k', 48));
        builder.UseSetting("Cors:AllowedOrigins:0", AllowedOrigin);
        _extra?.Invoke(builder);
    }

    /// <summary>Świeża baza dla każdego uruchomienia (deterministyczny stan: brak kont, brak seeda).</summary>
    private static void RecreateDatabase(string database)
    {
        var admin = new NpgsqlConnectionStringBuilder(BaseConnectionString) { Database = "postgres" };
        using var c = new NpgsqlConnection(admin.ConnectionString);
        c.Open();
        Exec(c, $"""
            DO $$ BEGIN
              IF NOT EXISTS (SELECT FROM pg_roles WHERE rolname = '{PilotRole}') THEN
                CREATE ROLE {PilotRole} LOGIN PASSWORD '{PilotPassword}';
              END IF;
            END $$;
            """);
        Exec(c, $"DROP DATABASE IF EXISTS {database} WITH (FORCE);");
        Exec(c, $"CREATE DATABASE {database} OWNER {PilotRole};");
    }

    private static void Exec(NpgsqlConnection c, string sql)
    {
        using var cmd = new NpgsqlCommand(sql, c);
        cmd.ExecuteNonQuery();
    }
}

[CollectionDefinition("api")]
public sealed class ApiCollection : ICollectionFixture<ApiFactory> { }

[CollectionDefinition("pilot")]
public sealed class PilotCollection : ICollectionFixture<PilotApiFactory> { }
