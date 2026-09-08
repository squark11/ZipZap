using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using ZipZap.BuildingBlocks.Messaging;
using ZipZap.BuildingBlocks.MultiTenancy;
using ZipZap.BuildingBlocks.Outbox;
using ZipZap.Contracts.Payments;
using ZipZap.Modules.Payments.Application;
using ZipZap.Modules.Payments.Infrastructure;

namespace ZipZap.Modules.Payments.Api;

public static class PaymentsEndpoints
{
    public static IEndpointRouteBuilder MapPaymentsEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/payments").WithTags("Payments");

        // --- Webhook dostawcy (bez auth — zabezpieczony podpisem) ---
        group.MapPost("/webhook/{provider}", async (
            string provider, HttpRequest request,
            PaymentsDbContext db, PaymentProviderRegistry providers, IIntegrationEventTypeRegistry events, CancellationToken ct) =>
        {
            var driver = providers.Get(provider);
            if (driver is null) return Results.NotFound();

            using var reader = new StreamReader(request.Body);
            var rawBody = await reader.ReadToEndAsync(ct);
            var signature = request.Headers["X-Signature"].ToString();

            var result = driver.VerifyWebhook(rawBody, signature);
            if (!result.Verified)
                return Results.Problem(detail: "Nieprawidłowy podpis webhooka.", statusCode: 400, title: "invalid_signature");

            var payment = await db.Payments.FirstOrDefaultAsync(p => p.Id == result.PaymentId, ct);
            if (payment is null) return Results.NotFound();

            if (result.Outcome == PaymentOutcome.Authorized)
            {
                if (payment.Authorize(result.ProviderRef)) // false = już rozstrzygnięta (idempotencja)
                {
                    db.AddOutboxMessage(new PaymentAuthorized(payment.OrderId, payment.StoreId, payment.Amount), events);
                    await db.SaveChangesAsync(ct);
                }
            }
            else if (result.Outcome == PaymentOutcome.Failed)
            {
                if (payment.Fail(result.ProviderRef))
                {
                    db.AddOutboxMessage(new PaymentFailed(payment.OrderId, payment.StoreId, "Płatność odrzucona przez dostawcę."), events);
                    await db.SaveChangesAsync(ct);
                }
            }

            return Results.Ok(new { status = payment.Status.ToString() });
        }).WithSummary("Webhook dostawcy płatności (autorytatywne źródło statusu).");

        // --- Odczyt płatności zamówienia (status + URL przekierowania) ---
        group.MapGet("/orders/{orderId:guid}", async (Guid orderId, PaymentsDbContext db, ICurrentUser user, CancellationToken ct) =>
        {
            var p = await db.Payments.AsNoTracking().FirstOrDefaultAsync(x => x.OrderId == orderId, ct);
            if (p is null) return Results.NotFound();
            if (!CanViewStore(user, p.StoreId)) return Forbidden();
            return Results.Ok(new
            {
                p.OrderId, p.StoreId, p.Amount, p.DeliveryFee, p.CommissionAmount,
                Status = p.Status.ToString(), p.Provider, p.RedirectUrl, p.ProviderRef,
            });
        }).RequireAuthorization();

        group.MapGet("/stores/{storeId:guid}/commission", async (Guid storeId, PaymentsDbContext db, ICurrentUser user, CancellationToken ct) =>
        {
            if (!CanViewStore(user, storeId)) return Forbidden();
            var total = await db.CommissionLedger.Where(l => l.StoreId == storeId).SumAsync(l => (decimal?)l.Amount, ct) ?? 0m;
            var count = await db.CommissionLedger.CountAsync(l => l.StoreId == storeId, ct);
            return Results.Ok(new { StoreId = storeId, TotalCommission = total, Entries = count });
        }).RequireAuthorization("StoreEmployee");

        return app;
    }

    private static bool CanViewStore(ICurrentUser user, Guid storeId)
        => user.Roles.Contains("Admin") || (user.Roles.Contains("StoreEmployee") && user.StoreId == storeId);

    private static IResult Forbidden()
        => Results.Problem(detail: "Brak dostępu do danych tego sklepu.", statusCode: 403, title: "forbidden");
}
