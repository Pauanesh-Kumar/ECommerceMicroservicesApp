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

// ── Customer-facing notification events, consumed by NotificationService ─────────────────────────
// NotificationService owns these wire shapes (flat camelCase JSON with an eventType field) and listens on
// the `order-placed` and `payment-processed` topics. Order publishes them because it is the service that
// holds the customer contact snapshot and observes every order/payment outcome through its saga.
public static class NotificationTopics
{
    public const string OrderPlaced = "order-placed";
    public const string PaymentProcessed = "payment-processed";
}

public sealed record OrderPlacedNotificationEvent(
    string EventType, Guid OrderId, Guid UserId, string Email, string? FirstName, string? LastName,
    decimal TotalAmount, string Currency, DateTime OccurredOn);

public sealed record PaymentNotificationEvent(
    string EventType, Guid PaymentId, Guid OrderId, Guid UserId, string Email, string? FirstName, string? LastName,
    decimal Amount, string Currency, bool Success, string? FailureReason, DateTime OccurredOn);
