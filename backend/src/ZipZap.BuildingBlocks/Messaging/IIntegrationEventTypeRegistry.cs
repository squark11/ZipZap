namespace ZipZap.BuildingBlocks.Messaging;

/// <summary>
/// Mapowanie stabilna-nazwa &lt;-&gt; typ CLR zdarzenia integracyjnego.
/// Używane przez outbox do (de)serializacji ładunku. Moduły rejestrują
/// swoje typy zdarzeń przy starcie.
/// </summary>
public interface IIntegrationEventTypeRegistry
{
    void Register<TEvent>() where TEvent : IIntegrationEvent;
    void Register(Type eventType);
    Type? Resolve(string typeName);
    string GetName(Type eventType);
}

public sealed class IntegrationEventTypeRegistry : IIntegrationEventTypeRegistry
{
    private readonly Dictionary<string, Type> _byName = new(StringComparer.Ordinal);

    public void Register<TEvent>() where TEvent : IIntegrationEvent => Register(typeof(TEvent));

    public void Register(Type eventType)
    {
        if (!typeof(IIntegrationEvent).IsAssignableFrom(eventType))
            throw new ArgumentException($"{eventType} nie jest zdarzeniem integracyjnym.", nameof(eventType));

        _byName[GetName(eventType)] = eventType;
    }

    public Type? Resolve(string typeName) => _byName.TryGetValue(typeName, out var type) ? type : null;

    // Stabilna nazwa = pełna nazwa typu (bez wersji assembly), odporna na bump wersji.
    public string GetName(Type eventType) => eventType.FullName!;
}
