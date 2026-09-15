namespace ZipZap.BuildingBlocks.Security;

/// <summary>
/// Weryfikuje token captchy przesłany przez klienta przy publicznych formularzach.
/// Gdy captcha nie jest skonfigurowana w panelu, implementacja zwraca <c>true</c>
/// (brak tarcia w dev i do czasu włączenia captchy) — inaczej wymaga poprawnego tokenu.
/// </summary>
public interface ICaptchaVerifier
{
    Task<bool> VerifyAsync(string? token, CancellationToken ct = default);
}
