using ZipZap.BuildingBlocks.Domain;

namespace ZipZap.Modules.Payments.Domain;

public enum PaymentStatus { Pending, Authorized, Failed, Settled, Refunded }

/// <summary>
/// Płatność za zamówienie. Autoryzacja następuje wyłącznie przez zweryfikowany
/// webhook dostawcy (nie na podstawie przekierowania klienta).
/// </summary>
public sealed class Payment : Entity
{
    public Guid OrderId { get; private set; }
    public Guid StoreId { get; private set; }
    public decimal Amount { get; private set; }
    public decimal DeliveryFee { get; private set; }
    public decimal CommissionAmount { get; private set; }
    public PaymentStatus Status { get; private set; }

    public string? Provider { get; private set; }
    public string? SessionId { get; private set; }
    public string? RedirectUrl { get; private set; }
    public string? ProviderRef { get; private set; }

    public DateTime CreatedAtUtc { get; private set; } = DateTime.UtcNow;
    public DateTime? AuthorizedAtUtc { get; private set; }

    private Payment() { } // EF

    public Payment(Guid orderId, Guid storeId, decimal amount, decimal deliveryFee, decimal commissionAmount)
    {
        OrderId = orderId;
        StoreId = storeId;
        Amount = amount;
        DeliveryFee = deliveryFee;
        CommissionAmount = commissionAmount;
        Status = PaymentStatus.Pending;
    }

    public void AttachSession(string provider, string sessionId, string redirectUrl)
    {
        Provider = provider;
        SessionId = sessionId;
        RedirectUrl = redirectUrl;
    }

    /// <summary>Autoryzacja z webhooka. Idempotentna: nie cofa stanu końcowego.</summary>
    public bool Authorize(string? providerRef)
    {
        if (Status != PaymentStatus.Pending) return false; // już rozstrzygnięta
        ProviderRef = providerRef;
        Status = PaymentStatus.Authorized;
        AuthorizedAtUtc = DateTime.UtcNow;
        return true;
    }

    public bool Fail(string? providerRef)
    {
        if (Status != PaymentStatus.Pending) return false;
        ProviderRef = providerRef;
        Status = PaymentStatus.Failed;
        return true;
    }

    public void Settle()
    {
        if (Status == PaymentStatus.Authorized) Status = PaymentStatus.Settled;
    }
}

/// <summary>Wpis księgi prowizji ZipZap↔sklep.</summary>
public sealed class CommissionLedgerEntry : Entity
{
    public Guid StoreId { get; private set; }
    public Guid OrderId { get; private set; }
    public decimal Amount { get; private set; }
    public DateTime CreatedAtUtc { get; private set; } = DateTime.UtcNow;

    private CommissionLedgerEntry() { } // EF

    public CommissionLedgerEntry(Guid storeId, Guid orderId, decimal amount)
    {
        StoreId = storeId;
        OrderId = orderId;
        Amount = amount;
    }
}
