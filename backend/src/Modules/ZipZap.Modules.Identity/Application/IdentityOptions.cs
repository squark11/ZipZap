namespace ZipZap.Modules.Identity.Application;

public sealed class IdentityOptions
{
    public const string SectionName = "Identity";

    /// <summary>Bazowy URL aplikacji (linki w e-mailach).</summary>
    public string PublicUrl { get; set; } = "http://localhost:4200";
    public int EmailVerificationHours { get; set; } = 24;
    public int PasswordResetHours { get; set; } = 1;
}
