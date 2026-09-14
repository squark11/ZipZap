using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using ZipZap.BuildingBlocks.Auditing;
using ZipZap.BuildingBlocks.Domain;
using ZipZap.Modules.Catalog.Application;

namespace ZipZap.Modules.Catalog.Api;

public static class CatalogEndpoints
{
    public static IEndpointRouteBuilder MapCatalogEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/catalog").WithTags("Catalog");

        // ---------- Publiczne (przeglądanie bez logowania) ----------

        group.MapGet("/stores", async (bool? onlyActive, double? lat, double? lng, CatalogService svc, CancellationToken ct) =>
            Results.Ok(await svc.ListStoresAsync(onlyActive ?? true, lat, lng, ct)));

        group.MapGet("/stores/{idOrSlug}", async (string idOrSlug, CatalogService svc, CancellationToken ct) =>
        {
            var result = await svc.GetStoreAsync(idOrSlug, ct);
            return result.IsSuccess ? Results.Ok(result.Value) : Problem(result.Error);
        });

        group.MapGet("/stores/{storeId:guid}/categories", async (Guid storeId, CatalogService svc, CancellationToken ct) =>
            Results.Ok(await svc.ListCategoriesAsync(storeId, ct)));

        group.MapGet("/stores/{storeId:guid}/products", async (Guid storeId, Guid? categoryId, CatalogService svc, CancellationToken ct) =>
            Results.Ok(await svc.ListProductsAsync(storeId, categoryId, ct)));

        group.MapGet("/products/{id:guid}", async (Guid id, CatalogService svc, CancellationToken ct) =>
        {
            var result = await svc.GetProductAsync(id, ct);
            return result.IsSuccess ? Results.Ok(result.Value) : Problem(result.Error);
        });

        // ---------- Zarządzanie sklepem (ADMIN) ----------

        // Re-emisja StoreUpdated dla wszystkich sklepów — odświeża read-modele
        // konsumentów (naprawa rekordów sprzed rozszerzenia zdarzeń o Status).
        group.MapPost("/admin/resync-projections", async (CatalogService svc, CancellationToken ct) =>
        {
            var result = await svc.ResyncStoreProjectionsAsync(ct);
            return result.IsSuccess
                ? Results.Ok(new { resynced = result.Value })
                : Problem(result.Error);
        }).RequireAuthorization("Admin");

        group.MapPost("/stores", async (CreateStoreRequest req, CatalogService svc, CancellationToken ct) =>
        {
            var result = await svc.CreateStoreAsync(
                req.Name, req.Slug, req.Description, req.City, req.Address, req.Phone,
                req.CommissionRate, req.MinimumOrderValue, ct, req.LogoUrl, req.Latitude, req.Longitude);
            return result.IsSuccess ? Results.Ok(result.Value) : Problem(result.Error);
        }).RequireAuthorization("Admin");

        group.MapPatch("/stores/{storeId:guid}", async (Guid storeId, UpdateStoreRequest req, CatalogService svc, IAuditLogger audit, CancellationToken ct) =>
        {
            var result = await svc.UpdateStoreAsync(storeId, req.CommissionRate, req.IsActive, req.Status, req.MinimumOrderValue, ct,
                req.LogoUrl, req.Latitude, req.Longitude);
            if (result.IsSuccess)
                await audit.LogAsync("store.updated", "store", storeId.ToString(), storeId,
                    new { req.CommissionRate, req.IsActive, req.Status, req.MinimumOrderValue, req.LogoUrl, req.Latitude, req.Longitude }, ct);
            return result.IsSuccess ? Results.Ok(result.Value) : Problem(result.Error);
        }).RequireAuthorization("StoreEmployee");

        // ---------- Oferta sklepu (STORE_EMPLOYEE / ADMIN) ----------

        group.MapPost("/stores/{storeId:guid}/categories", async (Guid storeId, CreateCategoryRequest req, CatalogService svc, CancellationToken ct) =>
        {
            var result = await svc.CreateCategoryAsync(storeId, req.Name, req.SortOrder, req.ParentId, ct);
            return result.IsSuccess ? Results.Ok(result.Value) : Problem(result.Error);
        }).RequireAuthorization("StoreEmployee");

        group.MapPost("/stores/{storeId:guid}/products", async (Guid storeId, CreateProductRequest req, CatalogService svc, CancellationToken ct) =>
        {
            var result = await svc.CreateProductAsync(
                storeId, req.CategoryId, req.Name, req.Description, req.Price,
                req.Currency, req.Unit, req.StockQty, req.ImageUrl, ct);
            return result.IsSuccess ? Results.Ok(result.Value) : Problem(result.Error);
        }).RequireAuthorization("StoreEmployee");

        group.MapPatch("/products/{id:guid}", async (Guid id, UpdateProductRequest req, CatalogService svc, CancellationToken ct) =>
        {
            var result = await svc.UpdateProductAsync(
                id, req.Name, req.Price, req.IsAvailable, req.CategoryId,
                req.StockQty, req.Description, req.ImageUrl, ct);
            return result.IsSuccess ? Results.Ok(result.Value) : Problem(result.Error);
        }).RequireAuthorization("StoreEmployee");

        // Import asortymentu z CSV. `commit=false` (domyślnie) = podgląd/walidacja bez zapisu;
        // `commit=true` = upsert produktów i kategorii po nazwie.
        group.MapPost("/stores/{storeId:guid}/products/import",
            async (Guid storeId, bool? commit, ImportProductsRequest req, CatalogService svc, IAuditLogger audit, CancellationToken ct) =>
        {
            var doCommit = commit ?? false;
            var result = await svc.ImportProductsAsync(storeId, req.Content, doCommit, ct);
            if (result.IsSuccess && doCommit)
                await audit.LogAsync("catalog.import", "store", storeId.ToString(), storeId,
                    new { result.Value.Total, result.Value.Created, result.Value.Updated, result.Value.Failed }, ct);
            return result.IsSuccess ? Results.Ok(result.Value) : Problem(result.Error);
        }).RequireAuthorization("StoreEmployee");

        // Eksport asortymentu do CSV (format zgodny z importem — round-trip: pobierz, edytuj, wgraj).
        group.MapGet("/stores/{storeId:guid}/products/export",
            async (Guid storeId, CatalogService svc, CancellationToken ct) =>
        {
            var result = await svc.ExportProductsAsync(storeId, ct);
            if (result.IsFailure) return Problem(result.Error);
            // Prefiks ﻿ = BOM UTF‑8 (poprawne polskie znaki w Excelu).
            var bytes = System.Text.Encoding.UTF8.GetBytes("﻿" + result.Value);
            var fileName = $"asortyment-{storeId:N}-{DateTime.UtcNow:yyyyMMdd}.csv";
            return Results.File(bytes, "text/csv; charset=utf-8", fileName);
        }).RequireAuthorization("StoreEmployee");

        return app;
    }

    private static IResult Problem(Error error)
        => Results.Problem(detail: error.Message, statusCode: error.ToStatusCode(), title: error.Code);
}
