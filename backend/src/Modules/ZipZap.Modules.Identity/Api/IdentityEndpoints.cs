using System.Security.Claims;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using ZipZap.BuildingBlocks.Domain;
using ZipZap.Modules.Identity.Application;
using ZipZap.Modules.Identity.Domain;

namespace ZipZap.Modules.Identity.Api;

public static class IdentityEndpoints
{
    public static IEndpointRouteBuilder MapIdentityEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/identity").WithTags("Identity");

        group.MapPost("/register", async (RegisterRequest req, IdentityService svc, CancellationToken ct) =>
        {
            var result = await svc.RegisterCustomerAsync(req.Email, req.Password, req.FullName, req.Phone, ct);
            return result.IsSuccess ? Results.Ok(ToResponse(result.Value)) : Problem(result.Error);
        });

        group.MapPost("/login", async (LoginRequest req, IdentityService svc, CancellationToken ct) =>
        {
            var result = await svc.LoginAsync(req.Email, req.Password, ct);
            return result.IsSuccess ? Results.Ok(ToResponse(result.Value)) : Problem(result.Error);
        });

        group.MapPost("/refresh", async (RefreshRequest req, IdentityService svc, CancellationToken ct) =>
        {
            var result = await svc.RefreshAsync(req.RefreshToken, ct);
            return result.IsSuccess ? Results.Ok(ToResponse(result.Value)) : Problem(result.Error);
        });

        group.MapPost("/google", async (GoogleLoginRequest req, IdentityService svc, CancellationToken ct) =>
        {
            var result = await svc.LoginWithGoogleAsync(req.IdToken, ct);
            return result.IsSuccess ? Results.Ok(ToResponse(result.Value)) : Problem(result.Error);
        })
        .WithSummary("Logowanie Google — klient przesyła zweryfikowany ID token.");

        group.MapGet("/me", (ClaimsPrincipal principal) =>
        {
            var id = principal.FindFirstValue("sub") ?? principal.FindFirstValue(ClaimTypes.NameIdentifier);
            var email = principal.FindFirstValue("email") ?? principal.FindFirstValue(ClaimTypes.Email);
            var name = principal.FindFirstValue("name");
            var roles = principal.FindAll(ClaimTypes.Role).Select(c => c.Value).Distinct().ToArray();
            return Results.Ok(new { id, email, name, roles });
        })
        .RequireAuthorization()
        .WithSummary("Dane bieżącego użytkownika (wymaga JWT).");

        group.MapPost("/admin/users", async (CreateUserRequest req, IdentityService svc, CancellationToken ct) =>
        {
            if (!Enum.TryParse<Role>(req.Role, ignoreCase: true, out var role))
                return Problem(Error.Validation($"Nieznana rola '{req.Role}'."));

            var result = await svc.CreateUserAsync(
                req.Email, req.Password, req.FullName, req.Phone, role, req.StoreId, ct);
            return result.IsSuccess ? Results.Ok(ToUser(result.Value)) : Problem(result.Error);
        })
        .RequireAuthorization("Admin")
        .WithSummary("Utworzenie użytkownika z rolą (tylko ADMIN).");

        group.MapGet("/admin/stores/{storeId:guid}/team", async (Guid storeId, IdentityService svc, CancellationToken ct) =>
            Results.Ok(await svc.ListStoreTeamAsync(storeId, ct)))
        .RequireAuthorization("Admin")
        .WithSummary("Zespół sklepu — pracownicy i kierowcy (tylko ADMIN).");

        group.MapPost("/logout", async (LogoutRequest req, IdentityService svc, CancellationToken ct) =>
        {
            var result = await svc.LogoutAsync(req.RefreshToken, ct);
            return result.IsSuccess ? Ok() : Problem(result.Error);
        })
        .RequireAuthorization()
        .WithSummary("Wylogowanie — unieważnia podany refresh token.");

        group.MapPost("/logout-all", async (ClaimsPrincipal principal, IdentityService svc, CancellationToken ct) =>
        {
            if (CurrentUserId(principal) is not Guid userId) return Problem(Error.Unauthorized("Brak tożsamości."));
            var result = await svc.RevokeAllTokensAsync(userId, ct);
            return result.IsSuccess ? Ok() : Problem(result.Error);
        })
        .RequireAuthorization()
        .WithSummary("Wyloguj wszędzie — unieważnia wszystkie refresh tokeny użytkownika.");

        group.MapPost("/password/change", async (ChangePasswordRequest req, ClaimsPrincipal principal, IdentityService svc, CancellationToken ct) =>
        {
            if (CurrentUserId(principal) is not Guid userId) return Problem(Error.Unauthorized("Brak tożsamości."));
            var result = await svc.ChangePasswordAsync(userId, req.CurrentPassword, req.NewPassword, ct);
            return result.IsSuccess ? Ok() : Problem(result.Error);
        })
        .RequireAuthorization()
        .WithSummary("Zmiana hasła (unieważnia wszystkie sesje).");

        group.MapPost("/admin/users/{userId:guid}/active", async (Guid userId, SetActiveRequest req, IdentityService svc, CancellationToken ct) =>
        {
            var result = await svc.SetUserActiveAsync(userId, req.IsActive, ct);
            return result.IsSuccess ? Ok() : Problem(result.Error);
        })
        .RequireAuthorization("Admin")
        .WithSummary("Blokada/odblokowanie konta (tylko ADMIN).");

        group.MapPost("/email/verify", async (VerifyEmailRequest req, IdentityService svc, CancellationToken ct) =>
        {
            var result = await svc.VerifyEmailAsync(req.Token, ct);
            return result.IsSuccess ? Ok() : Problem(result.Error);
        })
        .WithSummary("Potwierdzenie adresu e-mail tokenem z wiadomości.");

        group.MapPost("/email/resend-verification", async (ClaimsPrincipal principal, IdentityService svc, CancellationToken ct) =>
        {
            if (CurrentUserId(principal) is not Guid userId) return Problem(Error.Unauthorized("Brak tożsamości."));
            var result = await svc.ResendVerificationAsync(userId, ct);
            return result.IsSuccess ? Ok() : Problem(result.Error);
        })
        .RequireAuthorization()
        .WithSummary("Ponowne wysłanie e-maila weryfikacyjnego.");

        group.MapPost("/password/forgot", async (ForgotPasswordRequest req, IdentityService svc, CancellationToken ct) =>
        {
            await svc.ForgotPasswordAsync(req.Email, ct);
            return Ok(); // zawsze 200 — nie ujawnia istnienia konta
        })
        .WithSummary("Wysyła link resetu hasła (jeśli konto istnieje).");

        group.MapPost("/password/reset", async (ResetPasswordRequest req, IdentityService svc, CancellationToken ct) =>
        {
            var result = await svc.ResetPasswordAsync(req.Token, req.NewPassword, ct);
            return result.IsSuccess ? Ok() : Problem(result.Error);
        })
        .WithSummary("Ustawia nowe hasło tokenem resetu (unieważnia sesje).");

        return app;
    }

    private static AuthResponse ToResponse(AuthResult a)
        => new(a.AccessToken, a.AccessTokenExpiresAtUtc, a.RefreshToken, ToUser(a.User));

    private static UserResponse ToUser(UserDto u) => new(u.Id, u.Email, u.FullName, u.Roles, u.StoreIds);

    private static IResult Problem(Error error)
        => Results.Problem(detail: error.Message, statusCode: error.ToStatusCode(), title: error.Code);

    private static IResult Ok() => Results.Ok(new { status = "ok" });

    private static Guid? CurrentUserId(ClaimsPrincipal principal)
        => Guid.TryParse(principal.FindFirstValue("sub") ?? principal.FindFirstValue(ClaimTypes.NameIdentifier), out var id)
            ? id : null;
}
