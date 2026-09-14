using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Xunit;

namespace ZipZap.Api.IntegrationTests;

[Collection("api")]
public sealed class CatalogImportTests
{
    private readonly ApiFactory _f;
    public CatalogImportTests(ApiFactory f) => _f = f;

    private sealed record RowResult(int row, string name, string action, string? error);
    private sealed record Report(bool committed, int total, int created, int updated, int failed, RowResult[] rows);
    private sealed record Prod(Guid id, string name, decimal price, string unit, bool isAvailable);

    private const string Header = "nazwa;kategoria;cena;jednostka;dostepny";

    private Task<HttpResponseMessage> ImportAsync(HttpClient c, Guid storeId, string csv, bool commit)
        => c.PostAsJsonAsync($"/api/catalog/stores/{storeId}/products/import?commit={commit}", new { content = csv });

    [Fact]
    public async Task DryRun_reports_rows_without_persisting()
    {
        var admin = await _f.LoginAdminAsync();
        var storeId = Guid.Parse(await _f.CreateStoreAsync(admin.accessToken));
        var c = _f.Authed(admin.accessToken);

        var csv = string.Join("\n", Header,
            "Chleb żytni;Pieczywo;6,00;szt;tak",
            "Mleko 2%;Nabiał;3.49;szt;tak");

        var resp = await ImportAsync(c, storeId, csv, commit: false);
        resp.EnsureSuccessStatusCode();
        var report = (await resp.Content.ReadFromJsonAsync<Report>())!;

        report.committed.Should().BeFalse();
        report.total.Should().Be(2);
        report.created.Should().Be(2);
        report.failed.Should().Be(0);

        // Nic nie zapisano — lista produktów pusta.
        var products = await c.GetFromJsonAsync<Prod[]>($"/api/catalog/stores/{storeId}/products");
        products!.Should().BeEmpty();
    }

    [Fact]
    public async Task Commit_creates_then_updates_by_name()
    {
        var admin = await _f.LoginAdminAsync();
        var storeId = Guid.Parse(await _f.CreateStoreAsync(admin.accessToken));
        var c = _f.Authed(admin.accessToken);

        var first = string.Join("\n", Header,
            "Masło extra;Nabiał;8,99;szt;tak",
            "Sok pomarańczowy;Napoje;6,49;szt;nie");
        (await ImportAsync(c, storeId, first, commit: true)).EnsureSuccessStatusCode();

        var afterCreate = (await c.GetFromJsonAsync<Prod[]>($"/api/catalog/stores/{storeId}/products"))!;
        afterCreate.Should().HaveCount(2);
        afterCreate.Single(p => p.name == "Masło extra").price.Should().Be(8.99m);
        afterCreate.Single(p => p.name == "Sok pomarańczowy").isAvailable.Should().BeFalse();

        // Ponowny import tej samej nazwy = aktualizacja (upsert), nie duplikat.
        var second = string.Join("\n", Header, "Masło extra;Nabiał;10,50;szt;tak");
        var resp = await ImportAsync(c, storeId, second, commit: true);
        var report = (await resp.Content.ReadFromJsonAsync<Report>())!;
        report.updated.Should().Be(1);
        report.created.Should().Be(0);

        var afterUpdate = (await c.GetFromJsonAsync<Prod[]>($"/api/catalog/stores/{storeId}/products"))!;
        afterUpdate.Should().HaveCount(2); // wciąż 2 — bez duplikatu
        afterUpdate.Single(p => p.name == "Masło extra").price.Should().Be(10.50m);
    }

    [Fact]
    public async Task Invalid_rows_are_reported_and_skipped()
    {
        var admin = await _f.LoginAdminAsync();
        var storeId = Guid.Parse(await _f.CreateStoreAsync(admin.accessToken));
        var c = _f.Authed(admin.accessToken);

        var csv = string.Join("\n", Header,
            "Poprawny;Inne;5,00;szt;tak",
            ";Inne;3,00;szt;tak",              // brak nazwy
            "Zła cena;Inne;abc;szt;tak",       // nieprawidłowa cena
            "Poprawny;Inne;9,00;szt;tak");     // duplikat nazwy w pliku

        var resp = await ImportAsync(c, storeId, csv, commit: true);
        resp.EnsureSuccessStatusCode();
        var report = (await resp.Content.ReadFromJsonAsync<Report>())!;

        report.total.Should().Be(4);
        report.created.Should().Be(1);
        report.failed.Should().Be(3);
        report.rows.Where(r => r.action == "błąd").Should().HaveCount(3);

        var products = await c.GetFromJsonAsync<Prod[]>($"/api/catalog/stores/{storeId}/products");
        products!.Should().ContainSingle(p => p.name == "Poprawny");
    }

    [Fact]
    public async Task Other_store_employee_cannot_import()
    {
        var admin = await _f.LoginAdminAsync();
        var storeA = Guid.Parse(await _f.CreateStoreAsync(admin.accessToken));
        var storeB = Guid.Parse(await _f.CreateStoreAsync(admin.accessToken));
        var empB = await _f.CreateEmployeeAsync(admin.accessToken, storeB.ToString());

        var csv = string.Join("\n", Header, "Cokolwiek;Inne;1,00;szt;tak");
        var resp = await ImportAsync(_f.Authed(empB.accessToken), storeA, csv, commit: true);

        resp.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Export_returns_csv_and_roundtrips_through_import()
    {
        var admin = await _f.LoginAdminAsync();
        var storeId = Guid.Parse(await _f.CreateStoreAsync(admin.accessToken));
        var c = _f.Authed(admin.accessToken);

        var seed = string.Join("\n", Header,
            "Masło extra;Nabiał;8,99;szt;tak",
            "Chleb;Pieczywo;5,50;szt;nie");
        (await ImportAsync(c, storeId, seed, commit: true)).EnsureSuccessStatusCode();

        var resp = await c.GetAsync($"/api/catalog/stores/{storeId}/products/export");
        resp.EnsureSuccessStatusCode();
        resp.Content.Headers.ContentType!.MediaType.Should().Be("text/csv");
        var csv = await resp.Content.ReadAsStringAsync();

        csv.Should().Contain("nazwa;kategoria;cena;jednostka;dostepny");
        csv.Should().Contain("Masło extra;Nabiał;8,99;szt;tak");
        csv.Should().Contain("Chleb;Pieczywo;5,50;szt;nie");

        // Round-trip: wgranie wyeksportowanego pliku = same aktualizacje, zero nowych/duplikatów.
        var report = (await (await ImportAsync(c, storeId, csv, commit: true))
            .Content.ReadFromJsonAsync<Report>())!;
        report.created.Should().Be(0);
        report.updated.Should().Be(2);
        report.failed.Should().Be(0);

        var products = (await c.GetFromJsonAsync<Prod[]>($"/api/catalog/stores/{storeId}/products"))!;
        products.Should().HaveCount(2);
    }

    [Fact]
    public async Task Other_store_employee_cannot_export()
    {
        var admin = await _f.LoginAdminAsync();
        var storeA = Guid.Parse(await _f.CreateStoreAsync(admin.accessToken));
        var storeB = Guid.Parse(await _f.CreateStoreAsync(admin.accessToken));
        var empB = await _f.CreateEmployeeAsync(admin.accessToken, storeB.ToString());

        var resp = await _f.Authed(empB.accessToken).GetAsync($"/api/catalog/stores/{storeA}/products/export");
        resp.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}
