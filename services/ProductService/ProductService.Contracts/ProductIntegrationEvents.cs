namespace ProductService.Contracts;

// Published by ProductService on topic "product-stock" (wrapped in EventEnvelope) —
// OrderService's saga strategies react to these. Must stay field-for-field identical
// to OrderService.Contracts.StockReserved/StockReservationFailedIntegrationEvent.
public sealed record StockReservedIntegrationEvent(
    Guid OrderId, IReadOnlyList<Guid> ReservedProductIds, DateTime OccurredOnUtc);

public sealed record StockReservationFailedIntegrationEvent(
    Guid OrderId, string Reason, IReadOnlyList<Guid> UnavailableProductIds, DateTime OccurredOnUtc);
