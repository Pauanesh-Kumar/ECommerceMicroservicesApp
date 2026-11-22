namespace PaymentService.Contracts;

// ── Consumed by Payment (topic: payment-requested, published by Order) ──────
public sealed record PaymentRequestedIntegrationEvent(
    Guid OrderId, Guid UserId, decimal Amount, string Currency, DateTime OccurredOnUtc);

// ── Published by Payment (topic: payment-status), consumed by Order ─────────
public sealed record PaymentProcessedIntegrationEvent(
    Guid OrderId, Guid PaymentId, string ProviderReference, DateTime OccurredOnUtc);
public sealed record PaymentFailedIntegrationEvent(
    Guid OrderId, Guid? PaymentId, string Reason, DateTime OccurredOnUtc);
