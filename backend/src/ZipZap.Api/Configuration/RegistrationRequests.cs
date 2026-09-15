namespace ZipZap.Api.Configuration;

/// Publiczna rejestracja sklepu (self-service „Załóż sklep"): konto właściciela + sklep.
public sealed record RegisterStoreRequest(
    string Email, string Password, string FullName, string? Phone,
    string StoreName, string City, string? CaptchaToken);

/// Publiczna rejestracja dostawcy (self-service „Zostań dostawcą") — konto do weryfikacji.
public sealed record RegisterDriverRequest(
    string Email, string Password, string FullName, string? Phone, string? CaptchaToken);

/// Administrator zatwierdza dostawcę i przypisuje go do sklepu.
public sealed record ApproveDriverRequest(Guid StoreId);
