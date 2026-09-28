using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using ZipZap.BuildingBlocks.Outbox;
using ZipZap.Contracts.Catalog;
using ZipZap.Modules.Catalog.Infrastructure;
using ZipZap.Modules.Ordering.Infrastructure;

namespace ZipZap.Api.IntegrationTests;

/// <summary>
/// Kolejność zdarzeń Catalog przy kilku instancjach: starsze zdarzenie, które skończy się PO nowszym (inna instancja
/// wzięła nowszą partię), nie cofa projekcji sklepu/produktu — obowiązuje monotoniczna wersja agregatu.
/// </summary>
[Collection("outbox")]
public sealed class OutboxEventOrderTests
{
    private const int BatchSize = OutboxProcessor<CatalogDbContext>.BatchSize;

    private readonly OutboxApiFactory _f;
    public OutboxEventOrderTests(OutboxApiFactory f) => _f = f;

    [Fact]
    public async Task An_older_catalog_event_finishing_after_a_newer_one_does_not_roll_back_the_projection()
    {
        var storeId = Guid.NewGuid();
        var productId = Guid.NewGuid();
        var t0 = DateTime.UtcNow.AddYears(-8);

        // Partia 1 (najstarsze): 17 zdarzeń-wypełniaczy (pierwsze zatrzymuje instancję A na bramce) + 3 starsze zdarzenia.
        var gated = await OutboxTestData.AddProbeAsync<CatalogDbContext>(_f, t0);
        var gate = (Entered: new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously),
                    Release: new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously));
        OutboxProbeHandler.Gates[OutboxTestData.EventId(gated)] = gate;
        for (var i = 1; i < BatchSize - 3; i++)
            await OutboxTestData.AddProbeAsync<CatalogDbContext>(_f, t0.AddSeconds(i));

        var t1 = t0.AddMinutes(1);
        var registered = await OutboxTestData.AddEventAsync<CatalogDbContext>(_f, new StoreRegistered(storeId, "Sklep A",
            "sklep-a", 0.10m, "Kraków", true, "Open", 10m, AggregateVersion: 1) { OccurredAtUtc = t1 });
        var storeV2 = await OutboxTestData.AddEventAsync<CatalogDbContext>(_f, new StoreUpdated(storeId, 0.12m, true,
            "Open", 20m, AggregateVersion: 2) { OccurredAtUtc = t1.AddSeconds(1) });
        var productV1 = await OutboxTestData.AddEventAsync<CatalogDbContext>(_f, new ProductUpdated(productId, storeId,
            "Stara nazwa", 10.00m, "PLN", "szt", true, AggregateVersion: 1) { OccurredAtUtc = t1.AddSeconds(2) });

        // Partia 2 (nowsze): poza pierwszą partią.
        var t2 = t0.AddMinutes(2);
        var storeV3 = await OutboxTestData.AddEventAsync<CatalogDbContext>(_f, new StoreUpdated(storeId, 0.15m, true,
            "Closed", 30m, AggregateVersion: 3) { OccurredAtUtc = t2 });
        var productV2 = await OutboxTestData.AddEventAsync<CatalogDbContext>(_f, new ProductUpdated(productId, storeId,
            "Nowa nazwa", 12.50m, "PLN", "kg", false, AggregateVersion: 2) { OccurredAtUtc = t2.AddSeconds(1) });

        // Instancja A bierze partię 1 i staje na bramce; instancja B bierze partię 2 i kończy ją PIERWSZA.
        var instanceA = Task.Run(() => ProcessCatalogAsync());
        await gate.Entered.Task.WaitAsync(TimeSpan.FromSeconds(15));
        await ProcessCatalogAsync(); // instancja B
        (await ProductViewAsync(productId))!.Price.Should().Be(12.50m, "nowsze zdarzenie zastosowane przez instancję B");

        gate.Release.SetResult(); // teraz starsze zdarzenia kończą się PO nowszych
        await instanceA.WaitAsync(TimeSpan.FromSeconds(15));

        var all = await OutboxTestData.MessagesAsync<CatalogDbContext>(_f, registered, storeV2, productV1, storeV3, productV2);
        all.Should().OnlyContain(m => m.ProcessedAtUtc != null && m.Attempts == 0);
        var done = all.ToDictionary(m => m.Id, m => m.ProcessedAtUtc!.Value);
        done[productV1].Should().BeAfter(done[productV2], "scenariusz: starsze zdarzenie skończyło się później");
        done[storeV2].Should().BeAfter(done[storeV3]);

        var product = (await ProductViewAsync(productId))!;
        (product.Name, product.Price, product.Unit, product.IsAvailable, product.SourceVersion)
            .Should().Be(("Nowa nazwa", 12.50m, "kg", false, 2), "starsza wersja produktu nie cofa nowszej");

        var store = (await StoreViewAsync(storeId))!;
        (store.Name, store.CommissionRate, store.MinimumOrderValue, store.Status, store.SourceVersion)
            .Should().Be(("Sklep A", 0.15m, 30m, "Closed", 3),
                "nazwa uzupełniona z rejestracji, reszta z najnowszej wersji — starsze zdarzenia jej nie cofnęły");
    }

    [Fact]
    public async Task Concurrent_edits_of_one_product_never_fail_with_500_and_the_projection_matches_the_catalog()
    {
        var admin = await _f.LoginAdminAsync();
        var storeId = await _f.CreateStoreAsync(admin.accessToken);
        var created = await _f.Authed(admin.accessToken).PostAsJsonAsync($"/api/catalog/stores/{storeId}/products",
            new { name = "Chleb razowy", price = 5.00m, unit = "szt" });
        created.EnsureSuccessStatusCode();
        var productId = (await created.Content.ReadFromJsonAsync<IdDto>())!.id;
        var versionBefore = (await SourceProductAsync(productId)).Version;
        versionBefore.Should().Be(1, "utworzenie produktu = wersja 1");

        var prices = new[] { 11.11m, 22.22m, 33.33m, 44.44m };
        var responses = await OrderScenario.FireTogetherAsync(prices
            .Select(p => (Func<Task<HttpResponseMessage>>)(() =>
                _f.Authed(admin.accessToken).PatchAsJsonAsync($"/api/catalog/products/{productId}", new { price = p })))
            .ToList());

        responses.Should().OnlyContain(r => r.StatusCode == HttpStatusCode.OK || r.StatusCode == HttpStatusCode.Conflict,
            "równoległa edycja kończy się konfliktem (odśwież), nigdy błędem serwera ani cichym nadpisaniem");
        responses.Should().Contain(r => r.StatusCode == HttpStatusCode.OK);

        for (var i = 0; i < 3; i++) await DeliveryScenario.FlushOutboxAsync(_f);

        var source = await SourceProductAsync(productId);
        var view = (await ProductViewAsync(productId))!;
        (view.Price, view.SourceVersion).Should().Be((source.Price, source.Version),
            "projekcja = stan katalogu (ta sama cena i wersja)");
        source.Version.Should().Be(versionBefore + responses.Count(r => r.StatusCode == HttpStatusCode.OK),
            "każda zatwierdzona zmiana podbija wersję dokładnie o 1");
    }

    private async Task<ZipZap.Modules.Catalog.Domain.Product> SourceProductAsync(Guid id)
    {
        using var scope = _f.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<CatalogDbContext>().Products.AsNoTracking()
            .IgnoreQueryFilters().SingleAsync(p => p.Id == id);
    }

    // ---------- Pomocnicze ----------

    private sealed record IdDto(Guid id);

    /// <summary>Jedna „instancja API": świeży scope i własny procesor outboxa Catalog.</summary>
    private async Task ProcessCatalogAsync()
    {
        using var scope = _f.Services.CreateScope();
        await scope.ServiceProvider.GetServices<IOutboxProcessor>().OfType<OutboxProcessor<CatalogDbContext>>()
            .Single().ProcessPendingAsync();
    }

    private async Task<ZipZap.Modules.Ordering.Domain.ReadModel.CatalogProductView?> ProductViewAsync(Guid id)
    {
        using var scope = _f.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<OrderingDbContext>().CatalogProducts.AsNoTracking()
            .SingleOrDefaultAsync(p => p.Id == id);
    }

    private async Task<ZipZap.Modules.Ordering.Domain.ReadModel.CatalogStoreView?> StoreViewAsync(Guid id)
    {
        using var scope = _f.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<OrderingDbContext>().CatalogStores.AsNoTracking()
            .SingleOrDefaultAsync(v => v.Id == id);
    }
}
