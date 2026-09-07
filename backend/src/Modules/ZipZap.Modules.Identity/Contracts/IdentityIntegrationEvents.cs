using ZipZap.BuildingBlocks.Messaging;

namespace ZipZap.Modules.Identity.Contracts;

/// <summary>
/// PUBLICZNY kontrakt: nowy klient zarejestrowany. Publikowany przez outbox,
/// konsumowany np. przez Notifications (powitanie).
/// </summary>
public sealed record CustomerRegistered(Guid UserId, string Email, string FullName) : IntegrationEvent;
