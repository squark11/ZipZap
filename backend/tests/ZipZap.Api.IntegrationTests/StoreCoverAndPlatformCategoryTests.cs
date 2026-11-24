using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Xunit;

namespace ZipZap.Api.IntegrationTests;

/// <summary>
/// Redesign E3-1b: okładka i opis dostawy sklepu (F-5) oraz wspólne kategorie platformy (F-6) — lista publiczna,
/// zarządzanie przez administratora, przypinanie kategorii sklepu i klucze kategorii na liście sklepów.
/// </summary>
[Collection("api")]
public sealed class StoreCoverAndPlatformCategoryTests
{
    private readonly ApiFactory _f;
    public StoreCoverAndPlatformCategoryTests(ApiFactory f) => _f = f;

    private sealed record Store(Guid id, string? coverUrl, string? deliveryNote, string[] platformCategories);
    private sealed record PlatformCategory(string key, string name, int sortOrder, bool isActive);
    private sealed record Category(Guid id, string name, string? platformCategoryKey);
    private sealed record Product(Guid id);

    [Fact]
    public async Task Public_list_has_the_eleven_seeded_categories_in_order()
    {
        var list = await _f.Anon().GetFromJsonAsync<List<PlatformCategory>>("/api/catalog/platform-categories");
        list!.Select(c => c.key).Should().ContainInOrder("owoce", "warzywa", "pieczywo", "nabial", "mieso");
        list.Should().Contain(c => c.key == "chemia" && c.name == "Chemia i higiena");
        list.Should().OnlyContain(c => c.isActive);
    }

    [Fact]
    public async Task Store_lists_platform_category_only_while_it_has_an_available_product()
    {
        var admin = await _f.LoginAdminAsync();
        var storeId = await _f.CreateStoreAsync(admin.accessToken);
        var emp = await _f.CreateEmployeeAsync(admin.accessToken, storeId);
        var c = _f.Authed(emp.accessToken);

        var cat = await (await c.PostAsJsonAsync($"/api/catalog/stores/{storeId}/categories",
            new { name = "Owoce i warzywa", sortOrder = 0, platformCategoryKey = "owoce" })).Content.ReadFromJsonAsync<Category>();
        cat!.platformCategoryKey.Should().Be("owoce");

        (await Get(storeId)).platformCategories.Should().BeEmpty("kategoria bez produktów nie pokazuje sklepu pod chipem");

        var prodResp = await c.PostAsJsonAsync($"/api/catalog/stores/{storeId}/products",
            new { categoryId = cat.id, name = "Jabłka", price = 4.99m, unit = "kg" });
        prodResp.EnsureSuccessStatusCode();
        var product = (await prodResp.Content.ReadFromJsonAsync<Product>())!;

        (await Get(storeId)).platformCategories.Should().Equal("owoce");
        var all = await _f.Anon().GetFromJsonAsync<List<Store>>("/api/catalog/stores");
        all!.Single(s => s.id.ToString() == storeId).platformCategories.Should().Equal("owoce");

        (await c.PatchAsync($"/api/catalog/products/{product.id}", JsonContent.Create(new { isAvailable = false })))
            .EnsureSuccessStatusCode();
        (await Get(storeId)).platformCategories.Should().BeEmpty("ukryty produkt się nie liczy");

        // Przepięcie i odpięcie kategorii sklepu.
        (await c.PatchAsync($"/api/catalog/products/{product.id}", JsonContent.Create(new { isAvailable = true })))
            .EnsureSuccessStatusCode();
        var moved = await (await c.PutAsJsonAsync($"/api/catalog/categories/{cat.id}/platform-category", new { key = "warzywa" }))
            .Content.ReadFromJsonAsync<Category>();
        moved!.platformCategoryKey.Should().Be("warzywa");
        (await Get(storeId)).platformCategories.Should().Equal("warzywa");
        (await c.PutAsJsonAsync($"/api/catalog/categories/{cat.id}/platform-category", new { key = "" }))
            .EnsureSuccessStatusCode();
        (await Get(storeId)).platformCategories.Should().BeEmpty();
    }

    [Fact]
    public async Task Pinning_rejects_unknown_key_and_other_stores_staff()
    {
        var admin = await _f.LoginAdminAsync();
        var storeA = await _f.CreateStoreAsync(admin.accessToken);
        var storeB = await _f.CreateStoreAsync(admin.accessToken);
        var empA = _f.Authed((await _f.CreateEmployeeAsync(admin.accessToken, storeA)).accessToken);
        var empB = _f.Authed((await _f.CreateEmployeeAsync(admin.accessToken, storeB)).accessToken);

        var cat = await (await empA.PostAsJsonAsync($"/api/catalog/stores/{storeA}/categories",
            new { name = "Pieczywo", sortOrder = 0 })).Content.ReadFromJsonAsync<Category>();

        (await empA.PutAsJsonAsync($"/api/catalog/categories/{cat!.id}/platform-category", new { key = "kosmetyki" }))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await empA.PostAsJsonAsync($"/api/catalog/stores/{storeA}/categories",
            new { name = "X", sortOrder = 1, platformCategoryKey = "kosmetyki" })).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await empB.PutAsJsonAsync($"/api/catalog/categories/{cat.id}/platform-category", new { key = "pieczywo" }))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await _f.Anon().PutAsJsonAsync($"/api/catalog/categories/{cat.id}/platform-category", new { key = "pieczywo" }))
            .StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Only_admin_manages_platform_categories_and_hidden_ones_leave_the_public_list()
    {
        var admin = _f.Authed((await _f.LoginAdminAsync()).accessToken);
        var adminTok = (await _f.LoginAdminAsync()).accessToken;
        var storeId = await _f.CreateStoreAsync(adminTok);
        var emp = _f.Authed((await _f.CreateEmployeeAsync(adminTok, storeId)).accessToken);

        (await emp.PatchAsync("/api/catalog/admin/platform-categories/dziecko", JsonContent.Create(new { isActive = false })))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await admin.PatchAsync("/api/catalog/admin/platform-categories/nieistnieje", JsonContent.Create(new { name = "X" })))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);

        try
        {
            var hidden = await (await admin.PatchAsync("/api/catalog/admin/platform-categories/dziecko",
                JsonContent.Create(new { isActive = false, name = "Dla dzieci" }))).Content.ReadFromJsonAsync<PlatformCategory>();
            hidden!.isActive.Should().BeFalse();
            hidden.name.Should().Be("Dla dzieci");

            var pub = await _f.Anon().GetFromJsonAsync<List<PlatformCategory>>("/api/catalog/platform-categories");
            pub!.Should().NotContain(c => c.key == "dziecko");
            var full = await admin.GetFromJsonAsync<List<PlatformCategory>>("/api/catalog/admin/platform-categories");
            full!.Should().Contain(c => c.key == "dziecko" && !c.isActive);
        }
        finally
        {
            // Baza testowa jest współdzielona między uruchomieniami — przywracamy stan startowy.
            (await admin.PatchAsync("/api/catalog/admin/platform-categories/dziecko",
                JsonContent.Create(new { isActive = true, name = "Dla dziecka" }))).EnsureSuccessStatusCode();
        }
    }

    [Fact]
    public async Task Store_cover_and_delivery_note_set_validate_and_clear()
    {
        var admin = await _f.LoginAdminAsync();
        var storeId = await _f.CreateStoreAsync(admin.accessToken);
        var emp = _f.Authed((await _f.CreateEmployeeAsync(admin.accessToken, storeId)).accessToken);

        (await emp.PatchAsync($"/api/catalog/stores/{storeId}", JsonContent.Create(new
        {
            coverUrl = "https://cdn.zipzap.pl/okladka.jpg",
            deliveryNote = "  Dostawa autem sklepu  ",
        }))).EnsureSuccessStatusCode();
        var s = await Get(storeId);
        s.coverUrl.Should().Be("https://cdn.zipzap.pl/okladka.jpg");
        s.deliveryNote.Should().Be("Dostawa autem sklepu");

        (await emp.PatchAsync($"/api/catalog/stores/{storeId}", JsonContent.Create(new { coverUrl = "javascript:alert(1)" })))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await emp.PatchAsync($"/api/catalog/stores/{storeId}", JsonContent.Create(new { deliveryNote = new string('x', 81) })))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest);

        // Brak pola = bez zmian; pusty łańcuch = wyczyść.
        (await emp.PatchAsync($"/api/catalog/stores/{storeId}", JsonContent.Create(new { status = "Open" }))).EnsureSuccessStatusCode();
        (await Get(storeId)).coverUrl.Should().Be("https://cdn.zipzap.pl/okladka.jpg");
        (await emp.PatchAsync($"/api/catalog/stores/{storeId}", JsonContent.Create(new { coverUrl = "", deliveryNote = "" })))
            .EnsureSuccessStatusCode();
        s = await Get(storeId);
        s.coverUrl.Should().BeNull();
        s.deliveryNote.Should().BeNull();
    }

    private async Task<Store> Get(string storeId)
        => (await _f.Anon().GetFromJsonAsync<Store>($"/api/catalog/stores/{storeId}"))!;
}
