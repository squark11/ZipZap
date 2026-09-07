namespace ZipZap.BuildingBlocks.Outbox;

/// <summary>
/// Przetwarza nieprzetworzone wiadomości outboxa JEDNEGO modułu.
/// Dispatcher iteruje po wszystkich zarejestrowanych procesorach.
/// </summary>
public interface IOutboxProcessor
{
    Task ProcessPendingAsync(CancellationToken ct = default);
}
