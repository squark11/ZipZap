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

        return app;
    }

    private static AuthResponse ToResponse(AuthResult a)
        => new(a.AccessToken, a.AccessTokenExpiresAtUtc, a.RefreshToken, ToUser(a.User));

    private static UserResponse ToUser(UserDto u) => new(u.Id, u.Email, u.FullName, u.Roles);

    private static IResult Problem(Error error)
        => Results.Problem(detail: error.Message, statusCode: error.ToStatusCode(), title: error.Code);
}
