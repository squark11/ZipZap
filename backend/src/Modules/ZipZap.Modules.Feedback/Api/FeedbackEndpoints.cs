using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using ZipZap.BuildingBlocks.Domain;
using ZipZap.Modules.Feedback.Application;

namespace ZipZap.Modules.Feedback.Api;

public sealed record SubmitFeedbackRequest(
    string Type, string Message, string? ContactEmail, Guid? StoreId,
    string? Screen, string? AppVersion, string? Platform);

public sealed record UpdateFeedbackStatusRequest(string Status);

public static class FeedbackEndpoints
{
    public static IEndpointRouteBuilder MapFeedbackEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/feedback").WithTags("Feedback");

        // Zgłoszenie uwagi — PUBLICZNE (id użytkownika dołączane, jeśli zalogowany). Ochrona captchą → P6.
        group.MapPost("/", async (SubmitFeedbackRequest req, FeedbackService svc, CancellationToken ct) =>
        {
            var result = await svc.SubmitAsync(req.Type, req.Message, req.ContactEmail, req.StoreId,
                req.Screen, req.AppVersion, req.Platform, ct);
            return result.IsSuccess ? Results.Ok(new { id = result.Value }) : Problem(result.Error);
        });

        // Lista uwag — Admin (wszystkie) lub pracownik (uwagi swoich sklepów).
        group.MapGet("/", async (Guid? storeId, string? status, int? take, FeedbackService svc, CancellationToken ct) =>
            Results.Ok(await svc.ListAsync(storeId, status, take ?? 200, ct)))
            .RequireAuthorization("StoreEmployee");

        // Zmiana statusu uwagi (New/InProgress/Closed).
        group.MapPatch("/{id:guid}", async (Guid id, UpdateFeedbackStatusRequest req, FeedbackService svc, CancellationToken ct) =>
        {
            var result = await svc.SetStatusAsync(id, req.Status, ct);
            return result.IsSuccess ? Results.Ok(new { status = "ok" }) : Problem(result.Error);
        }).RequireAuthorization("StoreEmployee");

        return app;
    }

    private static IResult Problem(Error error)
        => Results.Problem(detail: error.Message, statusCode: error.ToStatusCode(), title: error.Code);
}
