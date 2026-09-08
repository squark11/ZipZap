namespace ZipZap.Modules.Identity.Application;

public sealed class GoogleOptions
{
    public const string SectionName = "Google";

    /// <summary>OAuth Client ID (audience tokenu ID). Puste = logowanie Google wyłączone.</summary>
    public string ClientId { get; set; } = "";

    public bool Enabled => !string.IsNullOrWhiteSpace(ClientId);
}

/// <summary>Zweryfikowane dane użytkownika z tokenu ID Google.</summary>
public sealed record GoogleUserInfo(string Subject, string Email, string Name, bool EmailVerified);

/// <summary>
/// Port weryfikacji tokenu ID Google. Zwraca null gdy token jest nieprawidłowy
/// albo logowanie Google nie jest skonfigurowane. Umożliwia testy bez sieci.
/// </summary>
public interface IGoogleTokenValidator
{
    Task<GoogleUserInfo?> ValidateAsync(string idToken, CancellationToken ct = default);
}
