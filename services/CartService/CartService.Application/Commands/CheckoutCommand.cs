using CartService.Application.Common;
using CartService.Application.Interfaces;
using CartService.Domain.Repositories;
using MediatR;

namespace CartService.Application.Commands.Checkout;

/// <summary>
/// Checkout is a synchronous call to OrderService (not a Kafka event) precisely so the
/// client gets a concrete OrderId back in the HTTP response. Everything AFTER
/// order-creation (stock reservation, payment, confirmation) is the async Kafka saga —
/// Cart's job ends the moment the Order row exists. Idempotency-Key is forwarded 1:1 so
/// a retried checkout (flaky network, double-tap) never creates two orders.
///
/// Depends on IOrderServiceClient rather than IHttpClientFactory/HttpClient directly —
/// the actual HTTP call, retry policy, and circuit breaker are all an Infrastructure
/// concern (see CartService.Infrastructure.Clients.OrderServiceClient), same DIP
/// boundary already applied to IPaymentGateway/IOrderUnitOfWork elsewhere in this
/// solution. This handler never needs to know Polly exists.
/// </summary>
public sealed record CheckoutCommand(Guid UserId, string IdempotencyKey) : IRequest<CheckoutResult>;

public sealed class CheckoutCommandHandler(
    IRedisCartRepository cartRepo, IOrderServiceClient orderServiceClient) : IRequestHandler<CheckoutCommand, CheckoutResult>
{
    public async Task<CheckoutResult> Handle(CheckoutCommand cmd, CancellationToken ct)
    {
        var cart = await cartRepo.GetAsync(cmd.UserId, ct);
        if (cart.Items.Count == 0)
            return new CheckoutResult(false, null, "Cart is empty.");

        var items = cart.Items.Select(i => new OrderLineForCreation(i.ProductId, i.Name, i.UnitPrice, i.Quantity)).ToList();
        var result = await orderServiceClient.CreateOrderAsync(cmd.UserId, items, cmd.IdempotencyKey, ct);

        if (!result.Success)
            return new CheckoutResult(false, null, result.Error);

        await cartRepo.ClearAsync(cmd.UserId, ct);
        return new CheckoutResult(true, result.OrderId, null);
    }
}
