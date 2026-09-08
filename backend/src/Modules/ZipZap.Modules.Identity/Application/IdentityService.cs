using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
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
    private readonly IEmailSender _email;
    private readonly IdentityOptions _options;
    private readonly IGoogleTokenValidator _google;

    public IdentityService(
        IdentityDbContext db,
        IPasswordHasher hasher,
        ITokenService tokens,
        IIntegrationEventTypeRegistry eventRegistry,
        IEmailSender email,
        IOptions<IdentityOptions> options,
        IGoogleTokenValidator google)
    {
        _db = db;
        _hasher = hasher;
        _tokens = tokens;
        _eventRegistry = eventRegistry;
        _email = email;
        _options = options.Value;
        _google = google;
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

        var rawVerify = CreateToken(user.Id, UserTokenType.EmailVerification, _options.EmailVerificationHours);
        var auth = IssueTokens(user);
        await _db.SaveChangesAsync(ct);

        await SendVerificationEmailAsync(user.Email, rawVerify, ct);
        return auth;
    }

    public async Task<Result> VerifyEmailAsync(string rawToken, CancellationToken ct)
    {
        var token = await FindValidTokenAsync(rawToken, UserTokenType.EmailVerification, ct);
        if (token is null) return Result.Failure(Error.Validation("Nieprawidłowy lub wygasły token weryfikacji."));

        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == token.UserId, ct);
        if (user is null) return Result.Failure(Error.NotFound("Użytkownik nie istnieje."));

        user.MarkEmailVerified();
        token.Use();
        await _db.SaveChangesAsync(ct);
        return Result.Success();
    }

    public async Task<Result> ResendVerificationAsync(Guid userId, CancellationToken ct)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId, ct);
        if (user is null) return Result.Failure(Error.NotFound("Użytkownik nie istnieje."));
        if (user.IsEmailVerified) return Result.Success();

        var raw = CreateToken(user.Id, UserTokenType.EmailVerification, _options.EmailVerificationHours);
        await _db.SaveChangesAsync(ct);
        await SendVerificationEmailAsync(user.Email, raw, ct);
        return Result.Success();
    }

    /// <summary>Zawsze zwraca sukces — nie ujawnia, czy konto istnieje.</summary>
    public async Task<Result> ForgotPasswordAsync(string email, CancellationToken ct)
    {
        var normalized = User.Normalize(email);
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == normalized, ct);
        if (user is not null && user.IsActive)
        {
            var raw = CreateToken(user.Id, UserTokenType.PasswordReset, _options.PasswordResetHours);
            await _db.SaveChangesAsync(ct);

            var link = $"{_options.PublicUrl.TrimEnd('/')}/reset-password?token={raw}";
            await _email.SendAsync(new EmailMessage(user.Email, "ZipZap — reset hasła",
                $"Aby zresetować hasło, otwórz: {link}\nLink wygasa za {_options.PasswordResetHours} h. Jeśli to nie Ty — zignoruj tę wiadomość."), ct);
        }
        return Result.Success();
    }

    public async Task<Result> ResetPasswordAsync(string rawToken, string newPassword, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(newPassword) || newPassword.Length < 6)
            return Result.Failure(Error.Validation("Nowe hasło musi mieć co najmniej 6 znaków."));

        var token = await FindValidTokenAsync(rawToken, UserTokenType.PasswordReset, ct);
        if (token is null) return Result.Failure(Error.Validation("Nieprawidłowy lub wygasły token resetu."));

        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == token.UserId, ct);
        if (user is null) return Result.Failure(Error.NotFound("Użytkownik nie istnieje."));

        user.ChangePassword(_hasher.Hash(newPassword));
        token.Use();
        var sessions = await _db.RefreshTokens.Where(t => t.UserId == user.Id && t.RevokedAtUtc == null).ToListAsync(ct);
        foreach (var s in sessions) s.Revoke();

        await _db.SaveChangesAsync(ct);
        return Result.Success();
    }

    private string CreateToken(Guid userId, UserTokenType type, int hours)
    {
        var raw = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        _db.UserTokens.Add(new UserToken(userId, type, _tokens.HashRefreshToken(raw), DateTime.UtcNow.AddHours(hours)));
        return raw;
    }

    private async Task<UserToken?> FindValidTokenAsync(string rawToken, UserTokenType type, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(rawToken)) return null;
        var hash = _tokens.HashRefreshToken(rawToken);
        var token = await _db.UserTokens.FirstOrDefaultAsync(t => t.TokenHash == hash && t.Type == type, ct);
        return token is not null && token.IsValid ? token : null;
    }

    private Task SendVerificationEmailAsync(string email, string rawToken, CancellationToken ct)
    {
        var link = $"{_options.PublicUrl.TrimEnd('/')}/verify-email?token={rawToken}";
        return _email.SendAsync(new EmailMessage(email, "ZipZap — potwierdź adres e-mail",
            $"Witaj w ZipZap! Potwierdź adres e-mail: {link}\nLink wygasa za {_options.EmailVerificationHours} h."), ct);
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

    /// <summary>Logowanie Google: weryfikuje token ID, tworzy/łączy konto (e-mail potwierdzony), wydaje JWT.</summary>
    public async Task<Result<AuthResult>> LoginWithGoogleAsync(string idToken, CancellationToken ct)
    {
        var info = await _google.ValidateAsync(idToken, ct);
        if (info is null)
            return Result.Failure<AuthResult>(Error.Unauthorized("Nieprawidłowy token Google lub logowanie Google nie jest skonfigurowane."));
        if (!info.EmailVerified)
            return Result.Failure<AuthResult>(Error.Unauthorized("Adres e-mail Google nie jest potwierdzony."));

        var normalized = User.Normalize(info.Email);
        var user = await _db.Users.Include(u => u.Roles).FirstOrDefaultAsync(u => u.Email == normalized, ct);
        if (user is null)
        {
            user = User.Register(info.Email, _hasher.Hash(Guid.NewGuid().ToString("N")), info.Name, null, Role.Customer);
            user.MarkEmailVerified();
            _db.Users.Add(user);
            _db.AddOutboxMessage(new CustomerRegistered(user.Id, user.Email, user.FullName), _eventRegistry);
        }
        else if (!user.IsActive)
        {
            return Result.Failure<AuthResult>(Error.Unauthorized("Konto jest zablokowane."));
        }

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

    /// <summary>Wylogowanie: unieważnia podany refresh token.</summary>
    public async Task<Result> LogoutAsync(string? refreshToken, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(refreshToken)) return Result.Success();
        var hash = _tokens.HashRefreshToken(refreshToken);
        var token = await _db.RefreshTokens.FirstOrDefaultAsync(t => t.TokenHash == hash, ct);
        if (token is not null && token.IsActive)
        {
            token.Revoke();
            await _db.SaveChangesAsync(ct);
        }
        return Result.Success();
    }

    /// <summary>Unieważnia wszystkie aktywne refresh tokeny użytkownika (wyloguj wszędzie).</summary>
    public async Task<Result> RevokeAllTokensAsync(Guid userId, CancellationToken ct)
    {
        var tokens = await _db.RefreshTokens.Where(t => t.UserId == userId && t.RevokedAtUtc == null).ToListAsync(ct);
        foreach (var token in tokens) token.Revoke();
        if (tokens.Count > 0) await _db.SaveChangesAsync(ct);
        return Result.Success();
    }

    /// <summary>Zmiana hasła (uwierzytelniona) — po zmianie unieważnia wszystkie sesje.</summary>
    public async Task<Result> ChangePasswordAsync(Guid userId, string currentPassword, string newPassword, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(newPassword) || newPassword.Length < 6)
            return Result.Failure(Error.Validation("Nowe hasło musi mieć co najmniej 6 znaków."));

        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId, ct);
        if (user is null) return Result.Failure(Error.NotFound("Użytkownik nie istnieje."));
        if (!_hasher.Verify(currentPassword, user.PasswordHash))
            return Result.Failure(Error.Unauthorized("Nieprawidłowe bieżące hasło."));

        user.ChangePassword(_hasher.Hash(newPassword));
        var tokens = await _db.RefreshTokens.Where(t => t.UserId == userId && t.RevokedAtUtc == null).ToListAsync(ct);
        foreach (var token in tokens) token.Revoke();
        await _db.SaveChangesAsync(ct);
        return Result.Success();
    }

    /// <summary>Administracyjna blokada/odblokowanie konta — blokada unieważnia sesje.</summary>
    public async Task<Result> SetUserActiveAsync(Guid userId, bool isActive, CancellationToken ct)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId, ct);
        if (user is null) return Result.Failure(Error.NotFound("Użytkownik nie istnieje."));

        if (isActive) user.Activate();
        else user.Deactivate();

        if (!isActive)
        {
            var tokens = await _db.RefreshTokens.Where(t => t.UserId == userId && t.RevokedAtUtc == null).ToListAsync(ct);
            foreach (var token in tokens) token.Revoke();
        }
        await _db.SaveChangesAsync(ct);
        return Result.Success();
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
