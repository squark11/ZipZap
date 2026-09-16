namespace ZipZap.Modules.Identity.Domain;

/// <summary>
/// Zapisane dane logowania konta testowego — narzędzie administratora serwisu (ADMIN-ONLY).
/// Hasło przechowywane jawnie, bo admin musi je odczytać (to konta testowe klient/dostawca/sklep,
/// nie realni użytkownicy). Nie mylić z <see cref="User"/> (hasła realnych kont są hashowane).
/// </summary>
public sealed class TestCredential
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public string Label { get; private set; } = default!;
    public string Role { get; private set; } = default!;   // etykieta roli (Customer/Driver/StoreEmployee/…), dowolny tekst
    public string Email { get; private set; } = default!;
    public string Password { get; private set; } = default!;
    public string? Note { get; private set; }
    public DateTime CreatedAtUtc { get; private set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; private set; } = DateTime.UtcNow;

    private TestCredential() { } // EF

    public TestCredential(string label, string role, string email, string password, string? note)
    {
        Apply(label, role, email, password, note);
        CreatedAtUtc = DateTime.UtcNow;
    }

    public void Update(string label, string role, string email, string password, string? note)
        => Apply(label, role, email, password, note);

    private void Apply(string label, string role, string email, string password, string? note)
    {
        Label = label.Trim();
        Role = role.Trim();
        Email = email.Trim();
        Password = password;
        Note = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
        UpdatedAtUtc = DateTime.UtcNow;
    }
}
