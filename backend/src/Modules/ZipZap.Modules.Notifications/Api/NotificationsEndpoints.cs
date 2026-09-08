using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using ZipZap.BuildingBlocks.MultiTenancy;
using ZipZap.Modules.Notifications.Domain;
using ZipZap.Modules.Notifications.Infrastructure;

namespace ZipZap.Modules.Notifications.Api;

public sealed record RegisterDeviceRequest(string Token, string? Platform);

public static class NotificationsEndpoints
{
    public static IEndpointRouteBuilder MapNotificationsEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/notifications").WithTags("Notifications");

        // Podgląd administracyjny (wszystkie powiadomienia).
        group.MapGet("/", async (int? take, NotificationsDbContext db, CancellationToken ct) =>
        {
            var items = await db.Notifications.AsNoTracking()
                .OrderByDescending(n => n.CreatedAtUtc)
                .Take(Math.Clamp(take ?? 50, 1, 200))
                .Select(n => new { n.Id, n.RecipientUserId, n.Channel, n.Template, n.Payload, n.Status, n.IsRead, n.CreatedAtUtc })
                .ToListAsync(ct);
            return Results.Ok(items);
        }).RequireAuthorization("Admin");

        // Moje powiadomienia (in-app skrzynka zalogowanego użytkownika).
        group.MapGet("/mine", async (int? take, ICurrentUser user, NotificationsDbContext db, CancellationToken ct) =>
        {
            if (user.UserId is not Guid uid) return Results.Unauthorized();
            var items = await db.Notifications.AsNoTracking()
                .Where(n => n.RecipientUserId == uid)
                .OrderByDescending(n => n.CreatedAtUtc)
                .Take(Math.Clamp(take ?? 50, 1, 200))
                .Select(n => new { n.Id, n.Template, n.Payload, n.Status, n.IsRead, n.CreatedAtUtc, n.SentAtUtc })
                .ToListAsync(ct);
            return Results.Ok(items);
        }).RequireAuthorization();

        // Oznacz jako przeczytane (tylko własne).
        group.MapPost("/{id:guid}/read", async (Guid id, ICurrentUser user, NotificationsDbContext db, CancellationToken ct) =>
        {
            if (user.UserId is not Guid uid) return Results.Unauthorized();
            var n = await db.Notifications.FirstOrDefaultAsync(x => x.Id == id && x.RecipientUserId == uid, ct);
            if (n is null) return Results.NotFound();
            n.MarkRead();
            await db.SaveChangesAsync(ct);
            return Results.NoContent();
        }).RequireAuthorization();

        // Rejestracja tokenu urządzenia (push/FCM) dla zalogowanego użytkownika.
        group.MapPost("/devices", async (RegisterDeviceRequest req, ICurrentUser user, NotificationsDbContext db, CancellationToken ct) =>
        {
            if (user.UserId is not Guid uid) return Results.Unauthorized();
            if (string.IsNullOrWhiteSpace(req.Token)) return Results.BadRequest();

            var existing = await db.DeviceTokens.FirstOrDefaultAsync(t => t.Token == req.Token, ct);
            if (existing is null)
                db.DeviceTokens.Add(new DeviceToken(uid, req.Token.Trim(), (req.Platform ?? "android").Trim()));
            await db.SaveChangesAsync(ct);
            return Results.NoContent();
        }).RequireAuthorization();

        return app;
    }
}
