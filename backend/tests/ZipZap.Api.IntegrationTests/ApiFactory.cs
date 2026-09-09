using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace ZipZap.Api.IntegrationTests;

public sealed record AuthUser(string id, string email, string[] roles, string[] storeIds);
public sealed record AuthDto(string accessToken, string refreshToken, AuthUser user);

/// <summary>
/// Bootuje całe API (WebApplicationFactory) na osobnej bazie testowej. Wymaga
/// działającego Postgresa (Docker) na localhost:5432 — baza `zipzap_it` jest
/// tworzona i migrowana przy pierwszym starcie.
/// </summary>
public sealed class ApiFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development"); // pomija guard produkcyjny
        builder.UseSetting("ConnectionStrings:Postgres",
            Environment.GetEnvironmentVariable("ZIPZAP_TEST_POSTGRES")
            ?? "Host=localhost;Port=5432;Database=zipzap_it;Username=zipzap;Password=zipzap");
    }

    public HttpClient Anon() => CreateClient();

    public HttpClient Authed(string accessToken)
    {
        var c = CreateClient();
        c.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        return c;
    }

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
    public async Task<AuthDto> CreateEmployeeAsync(string adminToken, string storeId)
    {
        var email = $"emp-{Guid.NewGuid():N}@test.pl";
        var resp = await Authed(adminToken).PostAsJsonAsync("/api/identity/admin/users", new
        {
            email,
            password = "Passw0rd!",
            fullName = "IT Employee",
            role = "StoreEmployee",
            storeId,
        });
        resp.EnsureSuccessStatusCode();
        return await LoginAsync(email, "Passw0rd!");
    }
}

[CollectionDefinition("api")]
public sealed class ApiCollection : ICollectionFixture<ApiFactory> { }
