using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using ZipZap.BuildingBlocks.Messaging;
using ZipZap.Modules.Identity.Domain;
using ZipZap.Modules.Identity.Infrastructure;

namespace ZipZap.Modules.Identity.Application;

/// <summary>
/// Zadanie „wyślij link resetu hasła" w outboxie modułu Identity — trwałe: zapisane w bazie, przetrwa restart API,
/// a awaria poczty oznacza ponowienia (outbox: bez limitu, 10 s → maks. co 30 min). Ładunek to wyłącznie adres wpisany
/// w formularzu; token powstaje dopiero przy wysyłce, więc w bazie nigdy nie leży jawny token.
/// </summary>
public sealed record PasswordResetEmailRequested(string Email) : IntegrationEvent;

/// <summary>Zadanie „wyślij link potwierdzający adres e-mail" (rejestracja, ponowna wysyłka) — jw.</summary>
public sealed record VerificationEmailRequested(Guid UserId) : IntegrationEvent;

/// <summary>
/// Wysyłka e-maili z linkami (reset hasła, potwierdzenie adresu) jako handlery zadań z outboxa. Kolejność: zapis
/// hasha tokenu → wysyłka → przy błędzie usunięcie tokenu (nie dotarł do nikogo, nie liczy się do limitu) i wyjątek,
/// po którym outbox ponawia. Loguje wyłącznie outbox — kategorię i typ błędu, nigdy treść (link z tokenem).
/// Limit linków na konto na godzinę chroni skrzynkę przed zasypaniem — ponad limit zadanie kończy się bez wysyłki.
/// </summary>
public sealed class AccountEmails :
    IIntegrationEventHandler<PasswordResetEmailRequested>,
    IIntegrationEventHandler<VerificationEmailRequested>
{
    public const string ResetSubject = "Dowózka.pl — ustaw nowe hasło";
    public const string VerifySubject = "Dowózka.pl — potwierdź adres e-mail";

    private readonly IdentityDbContext _db;
    private readonly ITokenService _tokens;
    private readonly IEmailSender _email;
    private readonly IdentityOptions _options;
    private readonly EmailDeliveryStatus _status;

    public AccountEmails(IdentityDbContext db, ITokenService tokens, IEmailSender email,
        IOptions<IdentityOptions> options, EmailDeliveryStatus status)
    {
        _db = db;
        _tokens = tokens;
        _email = email;
        _options = options.Value;
        _status = status;
    }

    public async Task HandleAsync(PasswordResetEmailRequested request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.Email)) return;
        var normalized = User.Normalize(request.Email);
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == normalized, ct);
        if (user is null || !user.IsActive) return; // nieznany adres — nic nie wysyłamy (odpowiedź API była ta sama)
        if (await RecentTokensAsync(user.Id, UserTokenType.PasswordReset, ct) >= _options.PasswordResetMaxPerHour) return;

        var hours = _options.PasswordResetHours == 1 ? "1 godzinę" : $"{_options.PasswordResetHours} h";
        await SendWithTokenAsync(user, UserTokenType.PasswordReset, TimeSpan.FromHours(_options.PasswordResetHours),
            raw => new EmailMessage(user.Email, ResetSubject,
                $"""
                Dzień dobry,

                otrzymaliśmy prośbę o zmianę hasła do konta Dowózka.pl.
                Ustaw nowe hasło: {PasswordResetLink(raw)}

                Link jest ważny {hours} i działa jeden raz. Po zmianie hasła wylogujemy Cię ze wszystkich urządzeń.
                Jeśli to nie Ty — zignoruj tę wiadomość, hasło pozostanie bez zmian.
                """), ct);
    }

    public async Task HandleAsync(VerificationEmailRequested request, CancellationToken ct = default)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == request.UserId, ct);
        if (user is null || !user.IsActive || user.IsEmailVerified) return; // już potwierdzony — link niepotrzebny
        if (await RecentTokensAsync(user.Id, UserTokenType.EmailVerification, ct) >= _options.EmailVerificationMaxPerHour) return;

        await SendWithTokenAsync(user, UserTokenType.EmailVerification, TimeSpan.FromHours(_options.EmailVerificationHours),
            raw => new EmailMessage(user.Email, VerifySubject,
                $"""
                Dzień dobry,

                dziękujemy za założenie konta w Dowózka.pl. Potwierdź adres e-mail: {EmailVerificationLink(raw)}

                Link jest ważny {_options.EmailVerificationHours} h.
                Jeśli konto zostało założone bez Twojej wiedzy — zignoruj tę wiadomość.
                """), ct);
    }

    /// <summary>Adres strony resetu (wspólny dla kont panelu i klientów aplikacji); token we fragmencie adresu.</summary>
    public string PasswordResetLink(string rawToken) =>
        $"{IdentityService.PublicBaseUrl(_options.PublicUrl)}/reset-password#token={rawToken}";

    /// <summary>Adres strony potwierdzenia (wspólny dla wszystkich kont); token we fragmencie adresu.</summary>
    public string EmailVerificationLink(string rawToken) =>
        $"{IdentityService.PublicBaseUrl(_options.PublicUrl)}/verify-email#token={rawToken}";

    private Task<int> RecentTokensAsync(Guid userId, UserTokenType type, CancellationToken ct)
    {
        var since = DateTime.UtcNow.AddHours(-1);
        return _db.UserTokens.CountAsync(t => t.UserId == userId && t.Type == type && t.CreatedAtUtc > since, ct);
    }

    private async Task SendWithTokenAsync(User user, UserTokenType type, TimeSpan lifetime,
        Func<string, EmailMessage> build, CancellationToken ct)
    {
        var raw = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        var token = new UserToken(user.Id, type, _tokens.HashRefreshToken(raw), DateTime.UtcNow.Add(lifetime));
        _db.UserTokens.Add(token);
        await _db.SaveChangesAsync(ct); // link w e-mailu zawsze ma swój token w bazie

        try
        {
            await _email.SendAsync(build(raw), ct);
            _status.MarkSent();
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
        {
            _status.MarkFailed(ex.GetType().Name);
            // Token nie dotarł do nikogo — usuwamy go (nie blokuje limitu); jeśli się nie uda, po prostu wygaśnie.
            try
            {
                _db.UserTokens.Remove(token);
                await _db.SaveChangesAsync(CancellationToken.None);
            }
            catch (Exception)
            {
                // celowo bez logu treści — nieużyty token wygaśnie sam
            }
            throw; // outbox ponowi zadanie (nowy token przy kolejnej próbie)
        }
    }
}
