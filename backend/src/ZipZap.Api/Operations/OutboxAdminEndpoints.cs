using Microsoft.AspNetCore.Mvc;
using ZipZap.BuildingBlocks.Auditing;
using ZipZap.BuildingBlocks.MultiTenancy;
using ZipZap.BuildingBlocks.Outbox;

namespace ZipZap.Api.Operations;

/// <summary>
/// Panel administratora: odłożone i długo ponawiane zdarzenia outboxa ze WSZYSTKICH modułów + ręczne ponowienie
/// jednego odłożonego zdarzenia. Tylko rola Admin. Odpowiedzi zawierają wyłącznie metadane (moduł, typ, identyfikator,
/// czasy, liczba prób, kategoria błędu) — nigdy ładunku zdarzenia, treści wyjątków ani danych klientów.
/// </summary>
public static class OutboxAdminEndpoints
{
    private const int MaxItemsPerModule = 100;

    public static IEndpointRouteBuilder MapOutboxAdminEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/admin/outbox").WithTags("Admin").RequireAuthorization("Admin");

        group.MapGet("/", async ([FromServices] IEnumerable<IOutboxAdministration> modules, CancellationToken ct) =>
        {
            var overviews = new List<OutboxModuleOverview>();
            foreach (var m in modules.OrderBy(m => m.Module, StringComparer.Ordinal))
                overviews.Add(await m.GetOverviewAsync(MaxItemsPerModule, ct));

            return Results.Ok(new
            {
                longRetryAttempts = OutboxPolicy.LongRetryAttempts,
                maxRetryDelayMinutes = (int)OutboxPolicy.MaxRetryDelay.TotalMinutes,
                summary = new
                {
                    deadLettered = overviews.Sum(o => o.DeadLettered),
                    retrying = overviews.Sum(o => o.Retrying),
                    longRetrying = overviews.Sum(o => o.LongRetrying),
                },
                modules = overviews.Select(o => new { o.Module, o.DeadLettered, o.Retrying, o.LongRetrying }),
                deadLettered = overviews.SelectMany(o => o.DeadLetteredItems).OrderByDescending(i => i.DeadLetteredAtUtc),
                longRetrying = overviews.SelectMany(o => o.LongRetryingItems)
                    .OrderByDescending(i => i.Attempts).ThenBy(i => i.OccurredAtUtc),
            });
        }).WithSummary("Odłożone i długo ponawiane zdarzenia (tylko metadane, bez ładunku).");

        group.MapPost("/{module}/{messageId:guid}/retry", async (string module, Guid messageId, OutboxRetryRequest req,
            [FromServices] IEnumerable<IOutboxAdministration> modules, ICurrentUser user, IAuditLogger audit,
            CancellationToken ct) =>
        {
            var target = modules.FirstOrDefault(m => string.Equals(m.Module, module, StringComparison.OrdinalIgnoreCase));
            if (target is null)
                return Results.Problem(detail: "Nieznany moduł.", statusCode: 404, title: "not_found");
            if (req.ExpectedAttempts is not int expected)
                return Results.Problem(detail: "Brak oczekiwanej liczby prób (expectedAttempts) — odśwież listę.",
                    statusCode: 400, title: "validation");
            if (user.UserId is not Guid userId) return Results.Unauthorized();

            var result = await target.RequeueDeadLetteredAsync(messageId, expected, userId, ct);
            switch (result)
            {
                case OutboxRequeueResult.Requeued:
                    await audit.LogAsync("outbox.manual_retry", "outbox_message", messageId.ToString(), null,
                        new { module = target.Module, attemptsBefore = expected }, ct);
                    return Results.Ok(new
                    {
                        module = target.Module, id = messageId, requeued = true,
                        message = "Zdarzenie wróciło do kolejki — zostanie wysłane w ciągu kilku sekund.",
                    });
                case OutboxRequeueResult.NotFound:
                    return Results.Problem(detail: "Nie ma takiego zdarzenia.", statusCode: 404, title: "not_found");
                default:
                    return Results.Problem(
                        detail: "Zdarzenie nie jest już odłożone albo zmieniło stan (np. ponowił je ktoś inny) — odśwież listę.",
                        statusCode: 409, title: "conflict");
            }
        }).WithSummary("Ręczne ponowienie JEDNEGO odłożonego zdarzenia (warunkowo, z audytem autora i czasu).");

        return app;
    }
}

/// <summary>Liczba prób widoczna w panelu — ponowienie zadziała tylko, jeśli od tamtej chwili nic się nie zmieniło.</summary>
public sealed record OutboxRetryRequest(int? ExpectedAttempts);
