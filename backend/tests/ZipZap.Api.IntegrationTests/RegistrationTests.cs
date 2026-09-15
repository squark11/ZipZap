using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Xunit;

namespace ZipZap.Api.IntegrationTests;

/// <summary>
/// Rejestracja per-kanał (P-Role2): sklepy i dostawcy rejestrują się przez WEB.
/// Klient rejestruje się z aplikacji mobilnej (`/api/identity/register`) — tu nietestowane.
/// </summary>
[Collection("api")]
public sealed class RegistrationTests
{
    private readonly ApiFactory _f;
    public RegistrationTests(ApiFactory f) => _f = f;

    private sealed record PendingDriver(string id, string email, string fullName, string role);

    [Fact]
    public async Task Store_self_registration_creates_owner_store_and_logs_in()
    {
        var email = $"store-{Guid.NewGuid():N}@test.pl";
        var storeName = $"Sklep {Guid.NewGuid():N}"[..16];
        var resp = await _f.Anon().PostAsJsonAsync("/api/register/store", new
        {
            email, password = "Passw0rd!", fullName = "Właściciel Sklepu", storeName, city = "Kraków", nip = "1234563218",
        });
        resp.EnsureSuccessStatusCode();
        var auth = (await resp.Content.ReadFromJsonAsync<AuthDto>())!;

        auth.user.roles.Should().Contain("StoreEmployee");
        auth.user.storeIds.Should().NotBeEmpty();

        // Token zawiera store_id → właściciel od razu zarządza swoim sklepem.
        var storeId = auth.user.storeIds[0];
        var cat = await _f.Authed(auth.accessToken).PostAsJsonAsync(
            $"/api/catalog/stores/{storeId}/categories", new { name = "Pieczywo", sortOrder = 0, parentId = (Guid?)null });
        cat.EnsureSuccessStatusCode();

        // Sklep jest widoczny na liście sklepów.
        var stores = await _f.Anon().GetFromJsonAsync<List<Dictionary<string, object>>>("/api/catalog/stores?onlyActive=false");
        stores!.Any(s => s["id"].ToString() == storeId).Should().BeTrue();
    }

    [Fact]
    public async Task Store_registration_rejects_duplicate_email()
    {
        var email = $"dup-{Guid.NewGuid():N}@test.pl";
        object Body() => new { email, password = "Passw0rd!", fullName = "X", storeName = "S" + Guid.NewGuid().ToString("N")[..6], city = "Y", nip = "1234563218" };
        (await _f.Anon().PostAsJsonAsync("/api/register/store", Body())).EnsureSuccessStatusCode();
        (await _f.Anon().PostAsJsonAsync("/api/register/store", Body())).StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Store_registration_rejects_invalid_nip()
    {
        var resp = await _f.Anon().PostAsJsonAsync("/api/register/store", new
        {
            email = $"badnip-{Guid.NewGuid():N}@test.pl", password = "Passw0rd!", fullName = "X",
            storeName = "S" + Guid.NewGuid().ToString("N")[..6], city = "Y", nip = "1112223334", // zła suma kontrolna
        });
        resp.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Driver_registration_is_pending_until_admin_approves()
    {
        var email = $"driver-{Guid.NewGuid():N}@test.pl";
        (await _f.Anon().PostAsJsonAsync("/api/register/driver",
            new { email, password = "Passw0rd!", fullName = "Kierowca Testowy" })).EnsureSuccessStatusCode();

        // Konto nieaktywne → logowanie zabronione dopóki admin nie zatwierdzi.
        (await _f.Anon().PostAsJsonAsync("/api/identity/login", new { email, password = "Passw0rd!" }))
            .StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        var admin = await _f.LoginAdminAsync();
        var pending = await _f.Authed(admin.accessToken).GetFromJsonAsync<List<PendingDriver>>("/api/admin/drivers/pending");
        var mine = pending!.FirstOrDefault(d => string.Equals(d.email, email, StringComparison.OrdinalIgnoreCase));
        mine.Should().NotBeNull();

        // Zatwierdzenie: przypisanie do sklepu + aktywacja → kierowca loguje się z rolą Driver.
        var storeId = await _f.CreateStoreAsync(admin.accessToken);
        (await _f.Authed(admin.accessToken).PostAsJsonAsync($"/api/admin/drivers/{mine!.id}/approve", new { storeId }))
            .EnsureSuccessStatusCode();

        var driverAuth = await _f.LoginAsync(email, "Passw0rd!");
        driverAuth.user.roles.Should().Contain("Driver");
        driverAuth.user.storeIds.Should().Contain(storeId);
    }
}
