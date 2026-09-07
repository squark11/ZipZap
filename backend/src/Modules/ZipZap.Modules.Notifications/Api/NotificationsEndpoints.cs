using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using ZipZap.Modules.Notifications.Infrastructure;

namespace ZipZap.Modules.Notifications.Api;

public static class NotificationsEndpoints
{
    public static IEndpointRouteBuilder MapNotificationsEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/notifications").WithTags("Notifications");

        group.MapGet("/", async (int? take, NotificationsDbContext db, CancellationToken ct) =>
        {
            var items = await db.Notifications.AsNoTracking()
                .OrderByDescending(n => n.CreatedAtUtc)
                .Take(Math.Clamp(take ?? 50, 1, 200))
                .Select(n => new { n.Id, n.RecipientUserId, n.Channel, n.Template, n.Payload, n.Status, n.CreatedAtUtc })
                .ToListAsync(ct);
            return Results.Ok(items);
        }).RequireAuthorization("Admin");

        return app;
    }
}
