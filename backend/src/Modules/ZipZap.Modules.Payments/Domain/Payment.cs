using ZipZap.BuildingBlocks.Domain;

namespace ZipZap.Modules.Payments.Domain;

public enum PaymentStatus { Pending, Authorized, Failed, Settled }

/// <summary>Płatność za zamówienie (szkielet — mockowa bramka).</summary>
public sealed class Payment : Entity
{
    public Guid OrderId { get; private set; }
    public Guid StoreId { get; private set; }
    public decimal Amount { get; private set; }
    public decimal CommissionAmount { get; private set; }
    public PaymentStatus Status { get; private set; }
    public string? ProviderRef { get; private set; }
    public DateTime CreatedAtUtc { get; private set; } = DateTime.UtcNow;

    private Payment() { } // EF

    public Payment(Guid orderId, Guid storeId, decimal amount, decimal commissionAmount)
    {
        OrderId = orderId;
        StoreId = storeId;
        Amount = amount;
        CommissionAmount = commissionAmount;
        Status = PaymentStatus.Pending;
    }

    public void Authorize(string providerRef)
    {
        ProviderRef = providerRef;
        Status = PaymentStatus.Authorized;
    }

    public void Settle() => Status = PaymentStatus.Settled;
    public void Fail() => Status = PaymentStatus.Failed;
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
