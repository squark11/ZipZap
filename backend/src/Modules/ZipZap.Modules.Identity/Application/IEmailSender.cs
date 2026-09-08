namespace ZipZap.Modules.Identity.Application;

public sealed record EmailMessage(string To, string Subject, string Body);

/// <summary>
/// Port wysyłki e-maili. Realny adapter (SMTP / dostawca) dodasz później
/// jako kolejną implementację; teraz działa mock logujący (bez SMTP).
/// </summary>
public interface IEmailSender
{
    Task SendAsync(EmailMessage message, CancellationToken ct = default);
}
