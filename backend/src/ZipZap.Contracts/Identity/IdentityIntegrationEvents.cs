using ZipZap.BuildingBlocks.Messaging;

namespace ZipZap.Contracts.Identity;

// Published language modułu Identity.

public sealed record CustomerRegistered(Guid UserId, string Email, string FullName) : IntegrationEvent;
