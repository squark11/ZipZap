namespace ZipZap.BuildingBlocks.Domain;

/// <summary>
/// Zdarzenie domenowe WEWNĄTRZ modułu (side-effect w tej samej granicy).
/// Do komunikacji MIĘDZY modułami służą zdarzenia integracyjne
/// (<see cref="ZipZap.BuildingBlocks.Messaging.IIntegrationEvent"/>).
/// </summary>
public interface IDomainEvent
{
    Guid Id { get; }
    DateTime OccurredAtUtc { get; }
}
