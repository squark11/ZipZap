using System.Linq;

namespace ZipZap.Api.Configuration;

/// Publiczna rejestracja sklepu (self-service „Załóż sklep"): konto właściciela + sklep.
public sealed record RegisterStoreRequest(
    string Email, string Password, string FullName, string? Phone,
    string StoreName, string City, string Nip, string? CaptchaToken);

/// Walidacja polskiego NIP-u (10 cyfr + suma kontrolna).
public static class NipValidator
{
    public static bool IsValid(string? nip)
    {
        var d = new string((nip ?? string.Empty).Where(char.IsDigit).ToArray());
        if (d.Length != 10) return false;
        int[] w = { 6, 5, 7, 2, 3, 4, 5, 6, 7 };
        var sum = 0;
        for (var i = 0; i < 9; i++) sum += (d[i] - '0') * w[i];
        var check = sum % 11;
        return check != 10 && check == d[9] - '0';
    }
}

/// Publiczna rejestracja dostawcy (self-service „Zostań dostawcą") — konto do weryfikacji.
public sealed record RegisterDriverRequest(
    string Email, string Password, string FullName, string? Phone, string? CaptchaToken);

/// Administrator zatwierdza dostawcę i przypisuje go do sklepu.
public sealed record ApproveDriverRequest(Guid StoreId);
