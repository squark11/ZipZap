using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using ZipZap.BuildingBlocks.Domain;
using ZipZap.BuildingBlocks.Logging;
using ZipZap.BuildingBlocks.Messaging;
using ZipZap.BuildingBlocks.Outbox;
using ZipZap.Contracts.Identity;
using ZipZap.Modules.Identity.Domain;
using ZipZap.Modules.Identity.Infrastructure;

namespace ZipZap.Modules.Identity.Application;

/// <summary>Stany linku resetu hasła (kod w polu „title" odpowiedzi problem+json, HTTP 400).</summary>
public static class PasswordResetErrors
{
    public static readonly Error Invalid = new("validation.reset_token_invalid",
        "Link do zmiany hasła jest nieprawidłowy lub niepełny. Skopiuj go z wiadomości w całości albo wyślij nowy.");
    public static readonly Error Expired = new("validation.reset_token_expired",
        "Link do zmiany hasła wygasł. Wyślij nowy link.");
    public static readonly Error Used = new("validation.reset_token_used",
        "Ten link został już użyty do zmiany hasła. Jeśli to nie Ty — wyślij nowy link i od razu zmień hasło.");
}

/// <summary>Stany linku potwierdzającego adres e-mail (kod w polu „title" odpowiedzi problem+json, HTTP 400).</summary>
public static class EmailVerificationErrors
{
    public static readonly Error Invalid = new("validation.verify_token_invalid",
        "Link potwierdzający adres e-mail jest nieprawidłowy lub niepełny. Skopiuj go z wiadomości w całości.");
    public static readonly Error Expired = new("validation.verify_token_expired",
        "Link potwierdzający adres e-mail wygasł. Wyślij nowy link.");
    /// <summary>Link zużyty albo adres potwierdzony już inaczej — w obu przypadkach nic więcej nie trzeba robić.</summary>
    public static readonly Error Used = new("validation.verify_token_used",
        "Ten adres e-mail jest już potwierdzony.");
    /// <summary>Ponowna wysyłka linku: wyczerpany limit linków na konto w ciągu godziny.</summary>
    public static readonly Error ResendLimit = new("validation.verify_resend_limit",
        "Wysłaliśmy już kilka linków w ciągu ostatniej godziny. Sprawdź skrzynkę (także Spam) albo spróbuj później.");
}

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
    private readonly IdentityOptions _options;
    private readonly IGoogleTokenValidator _google;
    private readonly ITotpService _totp;

    private const string TotpIssuer = "Dowozka.pl";
    private const int TwoFactorChallengeMinutes = 10;

    public IdentityService(
        IdentityDbContext db,
        IPasswordHasher hasher,
        ITokenService tokens,
        IIntegrationEventTypeRegistry eventRegistry,
        IOptions<IdentityOptions> options,
        IGoogleTokenValidator google,
        ITotpService totp)
    {
        _db = db;
        _hasher = hasher;
        _tokens = tokens;
        _eventRegistry = eventRegistry;
        _options = options.Value;
        _google = google;
        _totp = totp;
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
        // E-mail potwierdzający: zadanie w outboxie zapisane w tej samej transakcji co konto (trwałe, z ponowieniami).
        _db.AddOutboxMessage(new VerificationEmailRequested(user.Id), _eventRegistry);

        var auth = IssueTokens(user);
        await _db.SaveChangesAsync(ct);
        return auth;
    }

    /// <summary>
    /// Potwierdza adres tokenem z linku <c>{PublicUrl}/verify-email#token=…</c> (starsze maile: <c>?token=…</c>);
    /// zwraca zamaskowany adres konta. Token jest zużywany atomowo (równoległe kliknięcia — tylko jedno się uda),
    /// a pozostałe niewykorzystane linki potwierdzające konta przestają działać. Rozróżnienie „wygasł / użyty /
    /// nieprawidłowy" jest bezpieczne — zna je tylko posiadacz tokenu (256 bitów losowych).
    /// </summary>
    public async Task<Result<string>> VerifyEmailAsync(string? rawToken, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(rawToken) || rawToken.Length > 128) return EmailVerificationErrors.Invalid;
        var hash = _tokens.HashRefreshToken(rawToken.Trim());
        var token = await _db.UserTokens.AsNoTracking()
            .FirstOrDefaultAsync(t => t.TokenHash == hash && t.Type == UserTokenType.EmailVerification, ct);
        if (token is null) return EmailVerificationErrors.Invalid;
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == token.UserId, ct);
        if (user is null) return EmailVerificationErrors.Invalid;
        // Adres już potwierdzony (tym albo innym linkiem) — link nie jest potrzebny, niezależnie od terminu ważności.
        if (token.UsedAtUtc is not null || user.IsEmailVerified) return EmailVerificationErrors.Used;
        if (DateTime.UtcNow >= token.ExpiresAtUtc) return EmailVerificationErrors.Expired;

        await using var tx = await _db.Database.BeginTransactionAsync(ct);
        var now = DateTime.UtcNow;
        var claimed = await _db.UserTokens
            .Where(t => t.Id == token.Id && t.UsedAtUtc == null)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.UsedAtUtc, now), ct);
        if (claimed == 0) return EmailVerificationErrors.Used;

        await _db.UserTokens
            .Where(t => t.UserId == user.Id && t.Type == UserTokenType.EmailVerification && t.UsedAtUtc == null)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.UsedAtUtc, now), ct);

        user.MarkEmailVerified();
        await _db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return EmailLog.Mask(user.Email);
    }

    /// <summary>Czy adres e-mail konta jest potwierdzony (null — konto nie istnieje).</summary>
    public async Task<bool?> IsEmailVerifiedAsync(Guid userId, CancellationToken ct)
        => await _db.Users.AsNoTracking().Where(u => u.Id == userId).Select(u => (bool?)u.IsEmailVerified).FirstOrDefaultAsync(ct);

    /// <summary>
    /// Ponowna wysyłka linku potwierdzającego (zalogowany użytkownik, z panelu albo aplikacji). Zapisuje zadanie w
    /// outboxie; token powstaje dopiero przy wysyłce. Zwraca <c>false</c>, gdy adres jest już potwierdzony (nic nie
    /// wysyłamy). Maks. <see cref="IdentityOptions.EmailVerificationMaxPerHour"/> linków na konto na godzinę.
    /// </summary>
    public async Task<Result<bool>> ResendVerificationAsync(Guid userId, CancellationToken ct)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId, ct);
        if (user is null) return Error.NotFound("Użytkownik nie istnieje.");
        if (user.IsEmailVerified) return false;

        var since = DateTime.UtcNow.AddHours(-1);
        var recent = await _db.UserTokens.CountAsync(
            t => t.UserId == user.Id && t.Type == UserTokenType.EmailVerification && t.CreatedAtUtc > since, ct);
        if (recent >= _options.EmailVerificationMaxPerHour) return EmailVerificationErrors.ResendLimit;

        _db.AddOutboxMessage(new VerificationEmailRequested(user.Id), _eventRegistry);
        await _db.SaveChangesAsync(ct);
        return true;
    }

    /// <summary>
    /// Prośba o link resetu hasła: zapisuje WYŁĄCZNIE zadanie w outboxie (ten sam zapis dla istniejącego i
    /// nieistniejącego konta — odpowiedź i jej czas nie zdradzają, czy konto istnieje). Wyszukanie konta, limit na konto,
    /// token i wysyłka dzieją się w handlerze <see cref="AccountEmails"/>; zadanie jest trwałe (przetrwa restart API,
    /// przy awarii poczty outbox ponawia).
    /// </summary>
    public async Task RequestPasswordResetAsync(string? email, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(email) || email.Length > 320) return;
        _db.AddOutboxMessage(new PasswordResetEmailRequested(email.Trim()), _eventRegistry);
        await _db.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Baza linków w e-mailach z domeną IDN zapisaną w punycode (np. „panel.dowózka.pl" → „panel.xn--dowzka-dxa.pl") —
    /// klienci poczty nie zawsze rozpoznają link z polskimi znakami w domenie.
    /// </summary>
    public static string PublicBaseUrl(string publicUrl)
    {
        var raw = (publicUrl ?? string.Empty).Trim().TrimEnd('/');
        return Uri.TryCreate(raw, UriKind.Absolute, out var u)
            ? $"{u.Scheme}://{u.IdnHost}{(u.IsDefaultPort ? "" : ":" + u.Port)}{u.AbsolutePath.TrimEnd('/')}"
            : raw;
    }

    /// <summary>
    /// Sprawdza link resetu przed wpisaniem nowego hasła; zwraca zamaskowany adres konta (np. „j***@sklep.pl").
    /// Rozróżnienie „wygasł / użyty / nieprawidłowy" jest bezpieczne — zna je tylko posiadacz tokenu (256 bitów losowych).
    /// </summary>
    public async Task<Result<string>> CheckPasswordResetTokenAsync(string? rawToken, CancellationToken ct)
    {
        var (token, error) = await ResolvePasswordResetTokenAsync(rawToken, ct);
        if (error is not null) return error;
        var email = await _db.Users.Where(u => u.Id == token!.UserId).Select(u => u.Email).FirstOrDefaultAsync(ct);
        return email is null ? PasswordResetErrors.Invalid : EmailLog.Mask(email);
    }

    /// <summary>
    /// Ustawia nowe hasło tokenem z e-maila. Token jest zużywany atomowo (równoległe użycie tego samego linku — tylko
    /// jedno się uda), pozostałe niewykorzystane linki resetu konta przestają działać, a wszystkie sesje są unieważniane.
    /// </summary>
    public async Task<Result> ResetPasswordAsync(string? rawToken, string? newPassword, CancellationToken ct)
    {
        var (token, error) = await ResolvePasswordResetTokenAsync(rawToken, ct);
        if (error is not null) return Result.Failure(error);
        if (string.IsNullOrWhiteSpace(newPassword) || newPassword.Length < 6)
            return Result.Failure(Error.Validation("Nowe hasło musi mieć co najmniej 6 znaków."));

        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == token!.UserId, ct);
        if (user is null) return Result.Failure(PasswordResetErrors.Invalid);

        await using var tx = await _db.Database.BeginTransactionAsync(ct);
        var now = DateTime.UtcNow;
        var claimed = await _db.UserTokens
            .Where(t => t.Id == token!.Id && t.UsedAtUtc == null)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.UsedAtUtc, now), ct);
        if (claimed == 0) return Result.Failure(PasswordResetErrors.Used);

        await _db.UserTokens
            .Where(t => t.UserId == user.Id && t.Type == UserTokenType.PasswordReset && t.UsedAtUtc == null)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.UsedAtUtc, now), ct);

        user.ChangePassword(_hasher.Hash(newPassword));
        var sessions = await _db.RefreshTokens.Where(t => t.UserId == user.Id && t.RevokedAtUtc == null).ToListAsync(ct);
        foreach (var s in sessions) s.Revoke();

        await _db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return Result.Success();
    }

    private async Task<(UserToken? Token, Error? Error)> ResolvePasswordResetTokenAsync(string? rawToken, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(rawToken) || rawToken.Length > 128) return (null, PasswordResetErrors.Invalid);
        var hash = _tokens.HashRefreshToken(rawToken.Trim());
        var token = await _db.UserTokens.AsNoTracking()
            .FirstOrDefaultAsync(t => t.TokenHash == hash && t.Type == UserTokenType.PasswordReset, ct);
        if (token is null) return (null, PasswordResetErrors.Invalid);
        if (token.UsedAtUtc is not null) return (null, PasswordResetErrors.Used);
        if (DateTime.UtcNow >= token.ExpiresAtUtc) return (null, PasswordResetErrors.Expired);
        return (token, null);
    }

    private string CreateToken(Guid userId, UserTokenType type, TimeSpan lifetime)
    {
        var raw = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        _db.UserTokens.Add(new UserToken(userId, type, _tokens.HashRefreshToken(raw), DateTime.UtcNow.Add(lifetime)));
        return raw;
    }

    private async Task<UserToken?> FindValidTokenAsync(string rawToken, UserTokenType type, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(rawToken)) return null;
        var hash = _tokens.HashRefreshToken(rawToken);
        var token = await _db.UserTokens.FirstOrDefaultAsync(t => t.TokenHash == hash && t.Type == type, ct);
        return token is not null && token.IsValid ? token : null;
    }

    public async Task<Result<LoginResult>> LoginAsync(string email, string password, CancellationToken ct)
    {
        var normalized = User.Normalize(email);
        var user = await _db.Users
            .Include(u => u.Roles)
            .FirstOrDefaultAsync(u => u.Email == normalized, ct);

        if (user is null || !user.IsActive || !_hasher.Verify(password, user.PasswordHash))
            return Error.Unauthorized("Nieprawidłowy e-mail lub hasło.");

        // 2FA włączone → nie wydajemy tokenów; zwracamy krótkotrwałe wyzwanie do dokończenia kodem.
        if (user.TwoFactorEnabled)
        {
            var raw = CreateToken(user.Id, UserTokenType.TwoFactorChallenge,
                TimeSpan.FromMinutes(TwoFactorChallengeMinutes));
            await _db.SaveChangesAsync(ct);
            return LoginResult.Challenge(raw);
        }

        var auth = IssueTokens(user);
        await _db.SaveChangesAsync(ct);
        return LoginResult.Authenticated(auth);
    }

    /// <summary>Dokończenie logowania 2FA: weryfikuje wyzwanie + kod TOTP, wydaje tokeny.</summary>
    public async Task<Result<AuthResult>> CompleteTwoFactorLoginAsync(
        string challengeToken, string code, CancellationToken ct)
    {
        var token = await FindValidTokenAsync(challengeToken, UserTokenType.TwoFactorChallenge, ct);
        if (token is null) return Error.Unauthorized("Nieprawidłowe lub wygasłe wyzwanie 2FA — zaloguj się ponownie.");

        var user = await _db.Users.Include(u => u.Roles).FirstOrDefaultAsync(u => u.Id == token.UserId, ct);
        if (user is null || !user.IsActive) return Error.Unauthorized("Konto nieaktywne.");
        if (!user.TwoFactorEnabled || user.TwoFactorSecret is null)
            return Error.Unauthorized("2FA nie jest włączone dla tego konta.");

        if (!_totp.Verify(user.TwoFactorSecret, code))
            return Error.Unauthorized("Nieprawidłowy kod z aplikacji authenticator.");

        token.Use();
        var auth = IssueTokens(user);
        await _db.SaveChangesAsync(ct);
        return auth;
    }

    /// <summary>Rozpoczyna konfigurację 2FA: generuje sekret (jeszcze nieaktywny) i zwraca dane do QR.</summary>
    public async Task<Result<TwoFactorSetupDto>> BeginTwoFactorSetupAsync(Guid userId, CancellationToken ct)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId, ct);
        if (user is null) return Error.NotFound("Użytkownik nie istnieje.");
        if (user.TwoFactorEnabled) return Error.Conflict("2FA jest już włączone. Najpierw je wyłącz, aby zmienić.");

        var secret = _totp.GenerateSecret();
        user.SetTwoFactorSecret(secret);
        await _db.SaveChangesAsync(ct);

        return new TwoFactorSetupDto(secret, _totp.BuildOtpAuthUri(secret, user.Email, TotpIssuer));
    }

    /// <summary>Aktywuje 2FA po potwierdzeniu kodem z aplikacji authenticator.</summary>
    public async Task<Result> EnableTwoFactorAsync(Guid userId, string code, CancellationToken ct)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId, ct);
        if (user is null) return Result.Failure(Error.NotFound("Użytkownik nie istnieje."));
        if (user.TwoFactorEnabled) return Result.Success();
        if (user.TwoFactorSecret is null)
            return Result.Failure(Error.Validation("Najpierw rozpocznij konfigurację 2FA."));
        if (!_totp.Verify(user.TwoFactorSecret, code))
            return Result.Failure(Error.Validation("Nieprawidłowy kod — sprawdź aplikację authenticator i spróbuj ponownie."));

        user.EnableTwoFactor();
        await _db.SaveChangesAsync(ct);
        return Result.Success();
    }

    /// <summary>Wyłącza 2FA po potwierdzeniu kodem (potwierdzenie posiadania urządzenia).</summary>
    public async Task<Result> DisableTwoFactorAsync(Guid userId, string code, CancellationToken ct)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId, ct);
        if (user is null) return Result.Failure(Error.NotFound("Użytkownik nie istnieje."));
        if (!user.TwoFactorEnabled) return Result.Success();
        if (user.TwoFactorSecret is null || !_totp.Verify(user.TwoFactorSecret, code))
            return Result.Failure(Error.Unauthorized("Nieprawidłowy kod — nie można wyłączyć 2FA."));

        user.DisableTwoFactor();
        await _db.SaveChangesAsync(ct);
        return Result.Success();
    }

    /// <summary>Status 2FA konta (do panelu).</summary>
    public async Task<Result<bool>> TwoFactorStatusAsync(Guid userId, CancellationToken ct)
    {
        var user = await _db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == userId, ct);
        if (user is null) return Error.NotFound("Użytkownik nie istnieje.");
        return user.TwoFactorEnabled;
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

    /// <summary>
    /// Przypisuje istniejącemu użytkownikowi kolejny sklep jako pracownika (multi-lokalizacja).
    /// Idempotentne (domena deduplikuje rolę). Zwraca zaktualizowaną listę StoreIds użytkownika.
    /// </summary>
    public async Task<Result<UserDto>> AssignStoreEmployeeAsync(Guid userId, Guid storeId, CancellationToken ct)
    {
        var user = await _db.Users.Include(u => u.Roles).FirstOrDefaultAsync(u => u.Id == userId, ct);
        if (user is null) return Error.NotFound("Użytkownik nie istnieje.");
        user.AssignRole(Role.StoreEmployee, storeId);
        await _db.SaveChangesAsync(ct);
        return UserDto.From(user);
    }

    /// <summary>Czy adres e-mail jest wolny (sprawdzenie przed utworzeniem sklepu, by nie osierocić rekordu).</summary>
    public async Task<bool> IsEmailAvailableAsync(string email, CancellationToken ct)
        => !await _db.Users.AnyAsync(u => u.Email == User.Normalize(email), ct);

    /// <summary>
    /// Rejestracja właściciela sklepu (self-service „Załóż sklep"): konto <see cref="Role.StoreEmployee"/>
    /// przypisane do już utworzonego sklepu. Zwraca tokeny (od razu zalogowany) i wysyła e-mail weryfikacyjny.
    /// </summary>
    public async Task<Result<AuthResult>> RegisterStoreOwnerAsync(
        string email, string password, string fullName, string? phone, Guid storeId, CancellationToken ct)
    {
        var validation = Validate(email, password, fullName);
        if (validation is not null) return Error.Validation(validation);

        var normalized = User.Normalize(email);
        if (await _db.Users.AnyAsync(u => u.Email == normalized, ct))
            return Error.Conflict("Użytkownik z tym adresem e-mail już istnieje.");

        var user = User.Register(email, _hasher.Hash(password), fullName, phone, Role.StoreEmployee, storeId);
        _db.Users.Add(user);
        // E-mail potwierdzający: zadanie w outboxie zapisane w tej samej transakcji co konto (trwałe, z ponowieniami).
        _db.AddOutboxMessage(new VerificationEmailRequested(user.Id), _eventRegistry);

        var auth = IssueTokens(user);
        await _db.SaveChangesAsync(ct);
        return auth;
    }

    /// <summary>
    /// Rejestracja dostawcy (self-service „Zostań dostawcą"): konto <see cref="Role.Driver"/> BEZ sklepu,
    /// <b>nieaktywne</b> (do weryfikacji przez administratora serwisu). Nie loguje — czeka na aktywację.
    /// </summary>
    public async Task<Result> RegisterDriverAsync(
        string email, string password, string fullName, string? phone, CancellationToken ct)
    {
        var validation = Validate(email, password, fullName);
        if (validation is not null) return Result.Failure(Error.Validation(validation));

        var normalized = User.Normalize(email);
        if (await _db.Users.AnyAsync(u => u.Email == normalized, ct))
            return Result.Failure(Error.Conflict("Użytkownik z tym adresem e-mail już istnieje."));

        var user = User.Register(email, _hasher.Hash(password), fullName, phone, Role.Driver, storeId: null);
        user.Deactivate(); // pending — administrator aktywuje i przypisuje sklep
        _db.Users.Add(user);
        await _db.SaveChangesAsync(ct);
        return Result.Success();
    }

    /// <summary>Administrator: oczekujący dostawcy (rola Driver, konto nieaktywne).</summary>
    public async Task<IReadOnlyList<TeamMemberDto>> ListPendingDriversAsync(CancellationToken ct)
    {
        var users = await _db.Users.AsNoTracking().Include(u => u.Roles)
            .Where(u => !u.IsActive && u.Roles.Any(r => r.Role == Role.Driver))
            .OrderBy(u => u.CreatedAtUtc)
            .ToListAsync(ct);
        return users.Select(u => new TeamMemberDto(
            u.Id, u.Email, u.FullName, u.Phone, u.IsActive, u.IsEmailVerified, "Driver",
            u.Roles.FirstOrDefault(r => r.Role == Role.Driver && r.StoreId.HasValue)?.StoreId ?? Guid.Empty)).ToList();
    }

    /// <summary>Administrator: zatwierdza dostawcę — przypisuje do sklepu jako Driver i aktywuje konto.</summary>
    public async Task<Result> ApproveDriverAsync(Guid userId, Guid storeId, CancellationToken ct)
    {
        var user = await _db.Users.Include(u => u.Roles).FirstOrDefaultAsync(u => u.Id == userId, ct);
        if (user is null) return Result.Failure(Error.NotFound("Użytkownik nie istnieje."));
        if (!user.HasRole(Role.Driver)) return Result.Failure(Error.Validation("To konto nie jest kontem dostawcy."));
        user.AssignRole(Role.Driver, storeId);
        user.Activate();
        await _db.SaveChangesAsync(ct);
        return Result.Success();
    }

    /// <summary>Administrator serwisu: nadaje rolę istniejącemu użytkownikowi (i aktywuje konto).</summary>
    public async Task<Result> AssignRoleAsync(Guid userId, string roleName, Guid? storeId, CancellationToken ct)
    {
        if (!Enum.TryParse<Role>(roleName, ignoreCase: true, out var role))
            return Result.Failure(Error.Validation($"Nieznana rola '{roleName}'."));
        var user = await _db.Users.Include(u => u.Roles).FirstOrDefaultAsync(u => u.Id == userId, ct);
        if (user is null) return Result.Failure(Error.NotFound("Użytkownik nie istnieje."));
        user.AssignRole(role, storeId);
        if (!user.IsActive) user.Activate();
        await _db.SaveChangesAsync(ct);
        return Result.Success();
    }

    /// <summary>Aktywni kierowcy przypisani do sklepu (stan z bazy, nie z tokenu) — do przydziału dostaw.</summary>
    public async Task<IReadOnlyList<DriverRef>> ListActiveStoreDriversAsync(Guid storeId, CancellationToken ct)
        => await _db.Users.AsNoTracking()
            .Where(u => u.IsActive && u.Roles.Any(r => r.Role == Role.Driver && r.StoreId == storeId))
            .OrderBy(u => u.FullName)
            .Select(u => new DriverRef(u.Id, u.FullName))
            .ToListAsync(ct);

    /// <summary>Czy użytkownik jest TERAZ aktywnym kierowcą sklepu (konto aktywne + przypisanie do sklepu).</summary>
    public Task<bool> IsActiveDriverOfStoreAsync(Guid userId, Guid storeId, CancellationToken ct)
        => _db.Users.AsNoTracking().AnyAsync(u => u.Id == userId && u.IsActive
            && u.Roles.Any(r => r.Role == Role.Driver && r.StoreId == storeId), ct);

    /// <summary>Zespół sklepu: pracownicy i kierowcy przypisani do danego sklepu.</summary>
    public async Task<IReadOnlyList<TeamMemberDto>> ListStoreTeamAsync(Guid storeId, CancellationToken ct)
    {
        var users = await _db.Users.AsNoTracking().Include(u => u.Roles)
            .Where(u => u.Roles.Any(r => r.StoreId == storeId && (r.Role == Role.StoreEmployee || r.Role == Role.Driver)))
            .OrderBy(u => u.FullName)
            .ToListAsync(ct);

        return users.SelectMany(u => u.Roles
                .Where(r => r.StoreId == storeId && (r.Role == Role.StoreEmployee || r.Role == Role.Driver))
                .Select(r => new TeamMemberDto(u.Id, u.Email, u.FullName, u.Phone,
                    u.IsActive, u.IsEmailVerified, r.Role.ToString(), storeId)))
            .ToList();
    }

    /// <summary>Administrator serwisu: wszyscy użytkownicy (z rolami) — do tabeli i aktywacji.</summary>
    public async Task<IReadOnlyList<AdminUserDto>> ListUsersAsync(CancellationToken ct)
    {
        var users = await _db.Users.AsNoTracking().Include(u => u.Roles)
            .OrderByDescending(u => u.CreatedAtUtc)
            .ToListAsync(ct);
        return users.Select(u => new AdminUserDto(
            u.Id, u.Email, u.FullName, u.Phone, u.IsActive, u.IsEmailVerified,
            string.Join(", ", u.Roles.Select(r => r.Role.ToString()).Distinct()),
            u.CreatedAtUtc)).ToList();
    }

    /// <summary>Liczby użytkowników wg roli — do statystyk platformy.</summary>
    public async Task<UserCounts> UserCountsAsync(CancellationToken ct)
    {
        var users = await _db.Users.AsNoTracking().Include(u => u.Roles).ToListAsync(ct);
        return new UserCounts(
            users.Count,
            users.Count(u => u.HasRole(Role.Customer)),
            users.Count(u => u.HasRole(Role.StoreEmployee)),
            users.Count(u => u.HasRole(Role.Driver)),
            users.Count(u => !u.IsActive));
    }

    // ---- Zapisane dane kont testowych (ADMIN-ONLY, narzędzie administratora) ----

    public async Task<IReadOnlyList<TestCredentialDto>> ListTestCredentialsAsync(CancellationToken ct)
    {
        var items = await _db.TestCredentials.AsNoTracking().OrderBy(c => c.Label).ToListAsync(ct);
        return items.Select(TestCredentialDto.From).ToList();
    }

    /// <summary>Tworzy lub aktualizuje wpis (gdy podano Id). Zwraca zapisany wpis.</summary>
    public async Task<Result<TestCredentialDto>> SaveTestCredentialAsync(
        Guid? id, string label, string role, string email, string password, string? note, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(label)) return Error.Validation("Etykieta jest wymagana.");
        if (string.IsNullOrWhiteSpace(email)) return Error.Validation("E-mail jest wymagany.");
        if (string.IsNullOrWhiteSpace(password)) return Error.Validation("Hasło jest wymagane.");

        TestCredential entity;
        if (id is Guid gid)
        {
            var existing = await _db.TestCredentials.FirstOrDefaultAsync(c => c.Id == gid, ct);
            if (existing is null) return Error.NotFound("Wpis nie istnieje.");
            existing.Update(label, role, email, password, note);
            entity = existing;
        }
        else
        {
            entity = new TestCredential(label, role, email, password, note);
            _db.TestCredentials.Add(entity);
        }
        await _db.SaveChangesAsync(ct);
        return TestCredentialDto.From(entity);
    }

    public async Task<Result> DeleteTestCredentialAsync(Guid id, CancellationToken ct)
    {
        var existing = await _db.TestCredentials.FirstOrDefaultAsync(c => c.Id == id, ct);
        if (existing is null) return Result.Success(); // idempotentne
        _db.TestCredentials.Remove(existing);
        await _db.SaveChangesAsync(ct);
        return Result.Success();
    }

    // ---- Nadzór: zgłoszenia naruszeń regulaminu (ADMIN-ONLY) ----

    public async Task<IReadOnlyList<ComplianceFlagDto>> ListComplianceFlagsAsync(string? status, CancellationToken ct)
    {
        var q = _db.ComplianceFlags.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(status))
            q = q.Where(f => f.Status == status);
        var items = await q
            .OrderByDescending(f => f.Status == "Open")
            .ThenByDescending(f => f.CreatedAtUtc)
            .ToListAsync(ct);
        return items.Select(ComplianceFlagDto.From).ToList();
    }

    public async Task<ComplianceCounts> ComplianceCountsAsync(CancellationToken ct)
    {
        var flags = await _db.ComplianceFlags.AsNoTracking().ToListAsync(ct);
        return new ComplianceCounts(
            flags.Count(f => f.Status == "Open"),
            flags.Count(f => f.Status == "Resolved"),
            flags.Count(f => f.Status == "Open" && f.Severity == "high"));
    }

    public async Task<Result<ComplianceFlagDto>> CreateComplianceFlagAsync(
        string subjectType, Guid subjectId, string subjectLabel,
        string category, string severity, string? note, CancellationToken ct)
    {
        var st = (subjectType ?? "").Trim().ToLowerInvariant();
        if (st is not ("store" or "user")) return Error.Validation("Typ podmiotu musi być 'store' lub 'user'.");
        if (subjectId == Guid.Empty) return Error.Validation("Brak identyfikatora podmiotu.");
        if (string.IsNullOrWhiteSpace(subjectLabel)) return Error.Validation("Brak nazwy podmiotu.");

        var flag = new ComplianceFlag(st, subjectId, subjectLabel, category, severity, note);
        _db.ComplianceFlags.Add(flag);
        await _db.SaveChangesAsync(ct);
        return ComplianceFlagDto.From(flag);
    }

    public async Task<Result> ResolveComplianceFlagAsync(Guid id, string? resolution, bool reopen, CancellationToken ct)
    {
        var flag = await _db.ComplianceFlags.FirstOrDefaultAsync(f => f.Id == id, ct);
        if (flag is null) return Result.Failure(Error.NotFound("Zgłoszenie nie istnieje."));
        if (reopen) flag.Reopen(); else flag.Resolve(resolution);
        await _db.SaveChangesAsync(ct);
        return Result.Success();
    }

    public async Task<Result> DeleteComplianceFlagAsync(Guid id, CancellationToken ct)
    {
        var flag = await _db.ComplianceFlags.FirstOrDefaultAsync(f => f.Id == id, ct);
        if (flag is null) return Result.Success();
        _db.ComplianceFlags.Remove(flag);
        await _db.SaveChangesAsync(ct);
        return Result.Success();
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
