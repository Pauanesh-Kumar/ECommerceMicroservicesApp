namespace ProductService.Contracts;

// Published by OrderService on "order-created" after CreateOrderCommand commits (via its
// transactional Outbox). Consumed here to attempt an atomic stock reservation per line item.
// Must stay field-for-field identical to OrderService.Contracts.OrderCreatedIntegrationEvent.
public sealed record OrderCreatedIntegrationEvent(
    Guid OrderId,
    Guid UserId,
    IReadOnlyList<OrderCreatedLineItem> Items,
    DateTime OccurredOnUtc);

public sealed record OrderCreatedLineItem(Guid ProductId, int Quantity);

// Published by OrderService on "stock-release" as a saga compensation when payment fails
// or the order is cancelled after stock was already reserved.
public sealed record StockReleaseRequestedEvent(
    Guid OrderId,
    IReadOnlyList<OrderCreatedLineItem> Items,
    DateTime OccurredOnUtc);
