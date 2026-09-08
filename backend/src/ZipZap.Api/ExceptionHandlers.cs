using System.Diagnostics;
using Microsoft.AspNetCore.Diagnostics;

namespace ZipZap.Api;

/// <summary>
/// Zniekształcone/niepoprawne żądanie (np. błędny JSON, brak wymaganego pola)
/// → 400 z jednolitą kopertą, zamiast 500. Uzupełnia globalny ProblemDetails.
/// </summary>
public sealed class BadRequestExceptionHandler : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext ctx, Exception exception, CancellationToken ct)
    {
        if (exception is not BadHttpRequestException bad) return false;

        var status = bad.StatusCode is >= 400 and < 500 ? bad.StatusCode : StatusCodes.Status400BadRequest;
        ctx.Response.StatusCode = status;
        await ctx.Response.WriteAsJsonAsync(new
        {
            type = "about:blank",
            title = "bad_request",
            status,
            detail = "Nieprawidłowe żądanie (błędne lub niekompletne dane wejściowe).",
            traceId = Activity.Current?.Id ?? ctx.TraceIdentifier,
            code = "bad_request",
        }, ct);
        return true;
    }
}
