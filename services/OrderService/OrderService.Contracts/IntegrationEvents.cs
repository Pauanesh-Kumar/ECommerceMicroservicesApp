namespace OrderService.Contracts;

// ── Published by Order, consumed by Product ──────────────────────────────────
public sealed record OrderCreatedIntegrationEvent(
    Guid OrderId, Guid UserId, IReadOnlyList<OrderCreatedLineItem> Items, DateTime OccurredOnUtc);
public sealed record OrderCreatedLineItem(Guid ProductId, int Quantity);

public sealed record StockReleaseRequestedEvent(
    Guid OrderId, IReadOnlyList<OrderCreatedLineItem> Items, DateTime OccurredOnUtc);

// ── Consumed by Order, published by Product (topic: product-stock) ──────────
public sealed record StockReservedIntegrationEvent(
    Guid OrderId, IReadOnlyList<Guid> ReservedProductIds, DateTime OccurredOnUtc);
public sealed record StockReservationFailedIntegrationEvent(
    Guid OrderId, string Reason, IReadOnlyList<Guid> UnavailableProductIds, DateTime OccurredOnUtc);

// ── Published by Order, consumed by Payment (topic: payment-requested) ──────
public sealed record PaymentRequestedIntegrationEvent(
    Guid OrderId, Guid UserId, decimal Amount, string Currency, DateTime OccurredOnUtc);

// ── Consumed by Order, published by Payment (topic: payment-status) ─────────
public sealed record PaymentProcessedIntegrationEvent(
    Guid OrderId, Guid PaymentId, string ProviderReference, DateTime OccurredOnUtc);
public sealed record PaymentFailedIntegrationEvent(
    Guid OrderId, Guid? PaymentId, string Reason, DateTime OccurredOnUtc);

// ── Published by Order for downstream (Notification/Analytics already listen) ──
public sealed record OrderConfirmedIntegrationEvent(Guid OrderId, Guid UserId, decimal Amount, DateTime OccurredOnUtc);
public sealed record OrderCancelledIntegrationEvent(Guid OrderId, Guid UserId, string Reason, DateTime OccurredOnUtc);
