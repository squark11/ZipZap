using Microsoft.EntityFrameworkCore;
using ZipZap.BuildingBlocks.Domain;
using ZipZap.BuildingBlocks.Messaging;
using ZipZap.BuildingBlocks.Outbox;
using ZipZap.Contracts.Identity;
using ZipZap.Modules.Identity.Domain;
using ZipZap.Modules.Identity.Infrastructure;

namespace ZipZap.Modules.Identity.Application;

/// <summary>
/// Przypadki użycia modułu Identity. Publikacja zdarzeń idzie przez outbox
/// (zapis w tej samej transakcji co zmiana danych).
/// </summary>
public sealed class IdentityService
{
    private readonly IdentityDbContext _db;
    private readonly IPasswordHasher _hasher;
    private readonly ITokenService _tokens;
    private readonly IIntegrationEventTypeRegistry _eventRegistry;

    public IdentityService(
        IdentityDbContext db,
        IPasswordHasher hasher,
        ITokenService tokens,
        IIntegrationEventTypeRegistry eventRegistry)
    {
        _db = db;
        _hasher = hasher;
        _tokens = tokens;
        _eventRegistry = eventRegistry;
    }

    public async Task<Result<AuthResult>> RegisterCustomerAsync(
        string email, string password, string fullName, string? phone, CancellationToken ct)
    {
        var validation = Validate(email, password, fullName);
        if (validation is not null) return Error.Validation(validation);

        var normalized = User.Normalize(email);
        if (await _db.Users.AnyAsync(u => u.Email == normalized, ct))
            return Error.Conflict("Użytkownik z tym adresem e-mail już istnieje.");

        var user = User.Register(email, _hasher.Hash(password), fullName, phone, Role.Customer);
        _db.Users.Add(user);
        _db.AddOutboxMessage(new CustomerRegistered(user.Id, user.Email, user.FullName), _eventRegistry);

        var auth = IssueTokens(user);
        await _db.SaveChangesAsync(ct);
        return auth;
    }

    public async Task<Result<AuthResult>> LoginAsync(string email, string password, CancellationToken ct)
    {
        var normalized = User.Normalize(email);
        var user = await _db.Users
            .Include(u => u.Roles)
            .FirstOrDefaultAsync(u => u.Email == normalized, ct);

        if (user is null || !user.IsActive || !_hasher.Verify(password, user.PasswordHash))
            return Error.Unauthorized("Nieprawidłowy e-mail lub hasło.");

        var auth = IssueTokens(user);
        await _db.SaveChangesAsync(ct);
        return auth;
    }

    public async Task<Result<AuthResult>> RefreshAsync(string refreshToken, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(refreshToken))
            return Error.Unauthorized("Brak refresh tokena.");

        var hash = _tokens.HashRefreshToken(refreshToken);
        var existing = await _db.RefreshTokens.FirstOrDefaultAsync(t => t.TokenHash == hash, ct);
        if (existing is null || !existing.IsActive)
            return Error.Unauthorized("Nieprawidłowy lub wygasły refresh token.");

        var user = await _db.Users
            .Include(u => u.Roles)
            .FirstOrDefaultAsync(u => u.Id == existing.UserId, ct);
        if (user is null || !user.IsActive)
            return Error.Unauthorized("Konto nieaktywne.");

        var auth = IssueTokens(user);
        existing.Revoke(replacedByTokenId: auth.RefreshTokenId);
        await _db.SaveChangesAsync(ct);
        return auth;
    }

    /// <summary>Administracyjne utworzenie użytkownika z rolą (np. pracownik sklepu / kierowca).</summary>
    public async Task<Result<UserDto>> CreateUserAsync(
        string email, string password, string fullName, string? phone,
        Role role, Guid? storeId, CancellationToken ct)
    {
        var validation = Validate(email, password, fullName);
        if (validation is not null) return Error.Validation(validation);

        if (role is Role.StoreEmployee or Role.Driver && storeId is null)
            return Error.Validation("Rola pracownika/kierowcy wymaga StoreId.");

        var normalized = User.Normalize(email);
        if (await _db.Users.AnyAsync(u => u.Email == normalized, ct))
            return Error.Conflict("Użytkownik z tym adresem e-mail już istnieje.");

        var user = User.Register(email, _hasher.Hash(password), fullName, phone, role, storeId);
        _db.Users.Add(user);
        await _db.SaveChangesAsync(ct);
        return UserDto.From(user);
    }

    private AuthResult IssueTokens(User user)
    {
        var access = _tokens.CreateAccessToken(user);
        var refresh = _tokens.CreateRefreshToken();
        var entity = new RefreshToken(user.Id, refresh.Hash, refresh.ExpiresAtUtc);
        _db.RefreshTokens.Add(entity);

        return new AuthResult(
            access.Value,
            access.ExpiresAtUtc,
            refresh.Value,
            entity.Id,
            UserDto.From(user));
    }

    private static string? Validate(string email, string password, string fullName)
    {
        if (string.IsNullOrWhiteSpace(email) || !email.Contains('@'))
            return "Nieprawidłowy adres e-mail.";
        if (string.IsNullOrWhiteSpace(password) || password.Length < 6)
            return "Hasło musi mieć co najmniej 6 znaków.";
        if (string.IsNullOrWhiteSpace(fullName))
            return "Imię i nazwisko jest wymagane.";
        return null;
    }
}
