using System.Globalization;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using ZipZap.BuildingBlocks.Messaging;
using ZipZap.BuildingBlocks.MultiTenancy;
using ZipZap.BuildingBlocks.Outbox;
using ZipZap.Contracts.Payments;
using ZipZap.Modules.Payments.Application;
using ZipZap.Modules.Payments.Domain;
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

            var (verified, status) = await ApplyWebhookAsync(driver, rawBody, signature, db, events, ct);
            if (!verified)
                return Results.Problem(detail: "Nieprawidłowy podpis webhooka.", statusCode: 400, title: "invalid_signature");
            if (status is null) return Results.NotFound();
            return Results.Ok(new { status = status.ToString() });
        }).WithSummary("Webhook dostawcy płatności (autorytatywne źródło statusu).");

        // --- Dev: hostowana „strona płatności" mocka (zastępuje brakującą stronę panelu) ---
        // Tylko w Development. Klika sukces/porażkę → wysyła poprawnie PODPISANY webhook do
        // tej samej ścieżki (płatność pozostaje webhook-autorytatywna, bez fałszywego sukcesu).
        group.MapGet("/mock/pay", async (Guid payment, PaymentsDbContext db, IHostEnvironment env, CancellationToken ct) =>
        {
            if (!env.IsDevelopment()) return Results.NotFound();
            var p = await db.Payments.AsNoTracking().FirstOrDefaultAsync(x => x.Id == payment, ct);
            if (p is null) return Results.NotFound();
            return Results.Content(MockPayPageHtml(p.Id, p.Amount, p.Status.ToString()), "text/html; charset=utf-8");
        }).ExcludeFromDescription();

        group.MapPost("/mock/complete", async (
            HttpRequest request, PaymentsDbContext db, PaymentProviderRegistry providers,
            IIntegrationEventTypeRegistry events, IHostEnvironment env, IOptions<PaymentsOptions> opts, CancellationToken ct) =>
        {
            if (!env.IsDevelopment()) return Results.NotFound();
            var driver = providers.Get("mock");
            if (driver is null) return Results.NotFound();

            var form = await request.ReadFormAsync(ct);
            var paymentId = form["payment"].ToString();
            var outcome = form["outcome"].ToString().ToLowerInvariant();
            if (!Guid.TryParse(paymentId, out _) || (outcome != "authorized" && outcome != "failed"))
                return Results.BadRequest();

            // Zbuduj i PODPISZ webhook mocka — przechodzi przez tę samą weryfikację co realny.
            var body = JsonSerializer.Serialize(new { paymentId, outcome, providerRef = $"mock-{outcome}" });
            var signature = MockPaymentProvider.Sign(body, opts.Value.Mock.Secret);
            var (_, status) = await ApplyWebhookAsync(driver, body, signature, db, events, ct);

            return Results.Content(MockPayResultHtml(outcome == "authorized", status?.ToString()),
                "text/html; charset=utf-8");
        }).ExcludeFromDescription();

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

        // --- Odczyt WŁASNEJ płatności przez klienta (status + URL do zapłaty) ---
        // Płatność pozostaje webhook-autorytatywna: klient odpytuje status, nie ustawia go.
        group.MapGet("/orders/{orderId:guid}/mine", async (Guid orderId, PaymentsDbContext db, ICurrentUser user, CancellationToken ct) =>
        {
            if (user.UserId is not Guid uid) return Results.Unauthorized();
            var p = await db.Payments.AsNoTracking().FirstOrDefaultAsync(x => x.OrderId == orderId, ct);
            if (p is null) return Results.NotFound();
            if (p.CustomerId != uid) return Forbidden();
            return Results.Ok(new { p.OrderId, Status = p.Status.ToString(), p.RedirectUrl, p.Amount, p.DeliveryFee });
        }).RequireAuthorization();

        group.MapGet("/stores/{storeId:guid}/commission", async (Guid storeId, PaymentsDbContext db, ICurrentUser user, CancellationToken ct) =>
        {
            if (!CanViewStore(user, storeId)) return Forbidden();
            var total = await db.CommissionLedger.Where(l => l.StoreId == storeId).SumAsync(l => (decimal?)l.Amount, ct) ?? 0m;
            var count = await db.CommissionLedger.CountAsync(l => l.StoreId == storeId, ct);
            return Results.Ok(new { StoreId = storeId, TotalCommission = total, Entries = count });
        }).RequireAuthorization("StoreEmployee");

        // Podsumowanie płatności sklepu (liczniki wg statusu + kwoty).
        group.MapGet("/stores/{storeId:guid}/summary", async (Guid storeId, PaymentsDbContext db, ICurrentUser user, CancellationToken ct) =>
        {
            if (!CanViewStore(user, storeId)) return Forbidden();
            var payments = db.Payments.AsNoTracking().Where(p => p.StoreId == storeId);
            var pending = await payments.CountAsync(p => p.Status == PaymentStatus.Pending, ct);
            var paid = await payments.CountAsync(p => p.Status == PaymentStatus.Authorized || p.Status == PaymentStatus.Settled, ct);
            var settled = await payments.CountAsync(p => p.Status == PaymentStatus.Settled, ct);
            var failed = await payments.CountAsync(p => p.Status == PaymentStatus.Failed, ct);
            var grossPaid = await payments
                .Where(p => p.Status == PaymentStatus.Authorized || p.Status == PaymentStatus.Settled)
                .SumAsync(p => (decimal?)p.Amount, ct) ?? 0m;
            var settledCommission = await db.CommissionLedger.Where(l => l.StoreId == storeId).SumAsync(l => (decimal?)l.Amount, ct) ?? 0m;
            return Results.Ok(new { storeId, pending, paid, settled, failed, grossPaid, settledCommission });
        }).RequireAuthorization("StoreEmployee");

        // Księga prowizji sklepu (rozliczone zamówienia).
        group.MapGet("/stores/{storeId:guid}/ledger", async (Guid storeId, PaymentsDbContext db, ICurrentUser user, CancellationToken ct) =>
        {
            if (!CanViewStore(user, storeId)) return Forbidden();
            var entries = await db.CommissionLedger.AsNoTracking()
                .Where(l => l.StoreId == storeId)
                .OrderByDescending(l => l.CreatedAtUtc)
                .Take(200)
                .Select(l => new { l.OrderId, l.Amount, l.CreatedAtUtc })
                .ToListAsync(ct);
            return Results.Ok(entries);
        }).RequireAuthorization("StoreEmployee");

        return app;
    }

    private static bool CanViewStore(ICurrentUser user, Guid storeId)
        => user.Roles.Contains("Admin") || (user.Roles.Contains("StoreEmployee") && user.StoreId == storeId);

    private static IResult Forbidden()
        => Results.Problem(detail: "Brak dostępu do danych tego sklepu.", statusCode: 403, title: "forbidden");

    /// <summary>
    /// Wspólna obróbka zweryfikowanego webhooka: autoryzacja/odrzucenie + outbox.
    /// Zwraca (verified, statusPo) — status null gdy zweryfikowano, ale brak płatności.
    /// </summary>
    private static async Task<(bool verified, PaymentStatus? status)> ApplyWebhookAsync(
        IPaymentProvider driver, string rawBody, string? signature,
        PaymentsDbContext db, IIntegrationEventTypeRegistry events, CancellationToken ct)
    {
        var result = driver.VerifyWebhook(rawBody, signature);
        if (!result.Verified) return (false, null);

        var payment = await db.Payments.FirstOrDefaultAsync(p => p.Id == result.PaymentId, ct);
        if (payment is null) return (true, null);

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

        return (true, payment.Status);
    }

    private static string MockPayPageHtml(Guid paymentId, decimal amount, string status)
    {
        var pln = amount.ToString("0.00", CultureInfo.InvariantCulture).Replace('.', ',');
        var head = "<meta charset=\"utf-8\"><meta name=\"viewport\" content=\"width=device-width,initial-scale=1\">"
            + "<title>ZipZap — płatność (dev)</title><style>"
            + "body{margin:0;font:16px system-ui;background:#F7F8FA;color:#3A3F4B;display:flex;min-height:100vh;align-items:center;justify-content:center}"
            + ".card{background:#fff;border:1px solid #E5E7EB;border-radius:16px;padding:28px;max-width:360px;width:88%;text-align:center;box-shadow:0 8px 30px rgba(0,0,0,.06)}"
            + ".amt{font-size:34px;font-weight:800;color:#F97316;margin:6px 0 2px}"
            + ".mut{color:#6B7280;font-size:13px}button{width:100%;border:0;border-radius:10px;padding:14px;font-size:16px;font-weight:600;cursor:pointer;margin-top:10px}"
            + ".ok{background:#F97316;color:#fff}.no{background:#fff;color:#EF4444;border:1px solid #E5E7EB}"
            + ".badge{display:inline-block;background:#FFF3EA;color:#EA6A0C;border-radius:8px;padding:4px 10px;font-size:12px;font-weight:700;margin-bottom:10px}</style>";

        if (!string.Equals(status, "Pending", StringComparison.OrdinalIgnoreCase))
        {
            return $"<!doctype html><html lang=\"pl\"><head>{head}</head><body><div class=\"card\">"
                + "<div class=\"badge\">TRYB DEV</div>"
                + $"<h2>Płatność rozstrzygnięta</h2><p class=\"mut\">Status: {System.Net.WebUtility.HtmlEncode(status)}. Wróć do aplikacji ZipZap.</p>"
                + "</div></body></html>";
        }

        return $"<!doctype html><html lang=\"pl\"><head>{head}</head><body><div class=\"card\">"
            + "<div class=\"badge\">TRYB DEV — atrapa bramki</div>"
            + "<p class=\"mut\">Do zapłaty</p>"
            + $"<div class=\"amt\">{pln} zł</div>"
            + "<p class=\"mut\">To dev-owa strona płatności. Realny dostawca ma własną.</p>"
            + "<form method=\"post\" action=\"/api/payments/mock/complete\">"
            + $"<input type=\"hidden\" name=\"payment\" value=\"{paymentId}\">"
            + "<button class=\"ok\" name=\"outcome\" value=\"authorized\">Zapłać (sukces)</button>"
            + "<button class=\"no\" name=\"outcome\" value=\"failed\">Odrzuć płatność</button>"
            + "</form></div></body></html>";
    }

    private static string MockPayResultHtml(bool ok, string? status)
    {
        var color = ok ? "#22C55E" : "#EF4444";
        var title = ok ? "Płatność potwierdzona" : "Płatność odrzucona";
        return "<!doctype html><html lang=\"pl\"><head><meta charset=\"utf-8\">"
            + "<meta name=\"viewport\" content=\"width=device-width,initial-scale=1\"><title>ZipZap — płatność</title>"
            + "<style>body{margin:0;font:16px system-ui;background:#F7F8FA;color:#3A3F4B;display:flex;min-height:100vh;align-items:center;justify-content:center}"
            + ".card{background:#fff;border:1px solid #E5E7EB;border-radius:16px;padding:28px;max-width:360px;width:88%;text-align:center}"
            + $".dot{{width:56px;height:56px;border-radius:50%;background:{color};margin:0 auto 14px}}"
            + ".mut{color:#6B7280;font-size:14px}</style></head><body><div class=\"card\">"
            + "<div class=\"dot\"></div>"
            + $"<h2>{title}</h2>"
            + "<p class=\"mut\">Wróć do aplikacji ZipZap — status zaktualizuje się automatycznie.</p>"
            + "</div></body></html>";
    }
}
