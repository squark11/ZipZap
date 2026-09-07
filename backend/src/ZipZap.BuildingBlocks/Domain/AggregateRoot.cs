namespace ZipZap.BuildingBlocks.Domain;

/// <summary>
/// Korzeń agregatu — jedyny punkt wejścia do modyfikacji agregatu.
/// Gromadzi zdarzenia domenowe, które warstwa infrastruktury spłukuje do outboxa.
/// </summary>
public abstract class AggregateRoot : Entity
{
    private readonly List<IDomainEvent> _domainEvents = new();

    public IReadOnlyCollection<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();

    protected void Raise(IDomainEvent domainEvent) => _domainEvents.Add(domainEvent);

    public void ClearDomainEvents() => _domainEvents.Clear();
}
