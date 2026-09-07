namespace ZipZap.BuildingBlocks.Messaging;

/// <summary>
/// Zdarzenie integracyjne — PUBLICZNY kontrakt między modułami.
/// Publikowane przez outbox, konsumowane przez in-process event bus.
/// </summary>
public interface IIntegrationEvent
{
    Guid Id { get; }
    DateTime OccurredAtUtc { get; }
}

/// <summary>Bazowy rekord zdarzenia integracyjnego.</summary>
public abstract record IntegrationEvent : IIntegrationEvent
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public DateTime OccurredAtUtc { get; init; } = DateTime.UtcNow;
}
