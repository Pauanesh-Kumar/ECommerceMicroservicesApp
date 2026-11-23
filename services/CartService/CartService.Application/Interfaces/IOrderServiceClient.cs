namespace CartService.Application.Interfaces;

public sealed record OrderLineForCreation(Guid ProductId, string Name, decimal UnitPrice, int Quantity);
public sealed record CreateOrderClientResult(bool Success, Guid? OrderId, string? Error);

/// <summary>
/// Abstraction over "call OrderService to create an order" — Application depends only
/// on this, never on HttpClient/IHttpClientFactory/Polly directly. Infrastructure
/// provides the concrete implementation (OrderServiceClient), which owns the actual
/// HTTP call plus its retry/circuit-breaker policy. Same DIP boundary as
/// IPaymentGateway in PaymentService and IOrderUnitOfWork in OrderService.
/// </summary>
public interface IOrderServiceClient
{
    Task<CreateOrderClientResult> CreateOrderAsync(
        Guid userId, IReadOnlyList<OrderLineForCreation> items, string idempotencyKey, string accessToken, CancellationToken ct = default);
}
