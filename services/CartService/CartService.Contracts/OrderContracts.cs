namespace CartService.Contracts;

// Wire shape expected/returned by OrderService's POST /api/orders — kept field-for-field
// identical to OrderService.API's CreateOrderApiRequest/response, per this codebase's
// established cross-service-contract convention.
public sealed record CreateOrderLineRequest(Guid ProductId, string Name, decimal UnitPrice, int Quantity);
public sealed record CreateOrderRequest(Guid UserId, List<CreateOrderLineRequest> Items);
public sealed record CreateOrderResponse(Guid OrderId);
