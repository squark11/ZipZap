using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using ZipZap.Modules.Audit.Infrastructure;

namespace ZipZap.Modules.Audit.Api;

public static class AuditEndpoints
{
    public static IEndpointRouteBuilder MapAuditEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/audit").WithTags("Audit");

        // Rejestr akcji administracyjnych (tylko ADMIN). Filtry: storeId, action, take.
        group.MapGet("/", async (Guid? storeId, string? action, int? take, AuditDbContext db, CancellationToken ct) =>
        {
            var q = db.Entries.AsNoTracking().AsQueryable();
            if (storeId is Guid s) q = q.Where(e => e.StoreId == s);
            if (!string.IsNullOrWhiteSpace(action)) q = q.Where(e => e.Action == action);

            var items = await q
                .OrderByDescending(e => e.CreatedAtUtc)
                .Take(Math.Clamp(take ?? 100, 1, 500))
                .Select(e => new { e.Id, e.ActorUserId, e.Action, e.EntityType, e.EntityId, e.StoreId, e.Details, e.CreatedAtUtc })
                .ToListAsync(ct);

            return Results.Ok(items);
        }).RequireAuthorization("Admin");

        return app;
    }
}
