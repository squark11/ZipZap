namespace ZipZap.BuildingBlocks.Outbox;

/// <summary>
/// Budzi dispatcher outboxa od razu po zapisie pilnej wiadomości (np. zadania „wyślij e-mail z linkiem"), zamiast
/// czekać do kolejnego odpytania. To tylko przyspieszenie w tej instancji — bez sygnału (inna instancja, restart)
/// wiadomość i tak zostanie pobrana przy następnym cyklu.
/// </summary>
public sealed class OutboxSignal
{
    private readonly SemaphoreSlim _signal = new(0, 1);

    /// <summary>Wołaj PO zapisaniu wiadomości (po <c>SaveChanges</c>) — wcześniej dispatcher nic by nie znalazł.</summary>
    public void Notify()
    {
        try
        {
            if (_signal.CurrentCount == 0) _signal.Release();
        }
        catch (SemaphoreFullException)
        {
            // równoległe powiadomienie — dispatcher i tak jest już obudzony
        }
    }

    /// <summary>Czeka na sygnał albo upływ odstępu odpytywania (cokolwiek nastąpi pierwsze).</summary>
    public Task WaitAsync(TimeSpan pollInterval, CancellationToken ct) => _signal.WaitAsync(pollInterval, ct);
}
