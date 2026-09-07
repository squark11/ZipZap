using ZipZap.BuildingBlocks.Messaging;

namespace ZipZap.Contracts.Payments;

// Published language modułu Payments.

public sealed record PaymentAuthorized(Guid OrderId, Guid StoreId, decimal Amount) : IntegrationEvent;

public sealed record PaymentFailed(Guid OrderId, Guid StoreId, string Reason) : IntegrationEvent;
