namespace ZipZap.Modules.Identity.Infrastructure;

/// <summary>
/// Stan wysyłki e-maili z linkami od startu procesu (dla administratora) — same liczniki i kategoria ostatniego błędu,
/// bez adresów i treści. Trwałość zapewnia outbox; zadania czekające na ponowienie widać w panelu „Zdarzenia".
/// </summary>
public sealed class EmailDeliveryStatus
{
    private long _sent, _failed;
    public DateTime? LastSentAtUtc { get; private set; }
    public DateTime? LastFailureAtUtc { get; private set; }
    public string? LastFailureCategory { get; private set; }
    public long Sent => Interlocked.Read(ref _sent);
    public long Failed => Interlocked.Read(ref _failed);

    public void MarkSent() { Interlocked.Increment(ref _sent); LastSentAtUtc = DateTime.UtcNow; }
    public void MarkFailed(string category) { Interlocked.Increment(ref _failed); LastFailureAtUtc = DateTime.UtcNow; LastFailureCategory = category; }
}
