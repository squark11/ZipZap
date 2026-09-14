namespace ZipZap.Modules.Identity.Application;

/// <summary>
/// Źródło aktualnego Google OAuth Client ID (audience tokenu ID) w czasie działania.
/// Null/puste = logowanie Google wyłączone. Host może nadpisać domyślne (env) magazynem panelu.
/// </summary>
public interface IGoogleClientIdProvider
{
    Task<string?> GetClientIdAsync(CancellationToken ct = default);
}
