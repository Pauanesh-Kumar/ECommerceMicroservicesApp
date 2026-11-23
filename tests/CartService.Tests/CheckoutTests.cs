using CartService.Application.Commands.Checkout;
using CartService.Application.Interfaces;
using CartService.Domain.Entities;
using CartService.Domain.Repositories;

namespace CartService.Tests;

public class CheckoutTests
{
    [Fact]
    public async Task EmptyCartDoesNotCallOrderService()
    {
        var userId = Guid.NewGuid();
        var cart = new FakeCartRepository(new Cart(userId, []));
        var orders = new FakeOrderClient(new CreateOrderClientResult(true, Guid.NewGuid(), null));

        var result = await new CheckoutCommandHandler(cart, orders).Handle(new(userId, "key", "token"), default);

        Assert.False(result.Success);
        Assert.Equal("Cart is empty.", result.Error);
        Assert.False(orders.Called);
        Assert.False(cart.Cleared);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task CheckoutClearsCartOnlyAfterSuccessfulOrder(bool success)
    {
        var userId = Guid.NewGuid();
        var productId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var cart = new FakeCartRepository(new Cart(userId, [new(productId, "Item", 15m, 2)]));
        var orders = new FakeOrderClient(new CreateOrderClientResult(success, success ? orderId : null, success ? null : "failed"));

        var result = await new CheckoutCommandHandler(cart, orders).Handle(new(userId, "retry-key", "token"), default);

        Assert.Equal(success, result.Success);
        Assert.Equal(success, cart.Cleared);
        Assert.Equal("retry-key", orders.Key);
        Assert.Equal("token", orders.AccessToken);
        Assert.Equal(userId, orders.UserId);
        var item = Assert.Single(orders.Items!);
        Assert.Equal(productId, item.ProductId);
        Assert.Equal(2, item.Quantity);
        Assert.Equal(success ? orderId : null, result.OrderId);
    }

    private sealed class FakeCartRepository(Cart cart) : IRedisCartRepository
    {
        public bool Cleared { get; private set; }
        public Task<Cart> GetAsync(Guid userId, CancellationToken ct = default) => Task.FromResult(cart);
        public Task SaveAsync(Cart value, CancellationToken ct = default) => Task.CompletedTask;
        public Task ClearAsync(Guid userId, CancellationToken ct = default) { Cleared = true; return Task.CompletedTask; }
    }

    private sealed class FakeOrderClient(CreateOrderClientResult result) : IOrderServiceClient
    {
        public bool Called { get; private set; }
        public Guid UserId { get; private set; }
        public string? Key { get; private set; }
        public string? AccessToken { get; private set; }
        public IReadOnlyList<OrderLineForCreation>? Items { get; private set; }
        public Task<CreateOrderClientResult> CreateOrderAsync(Guid userId, IReadOnlyList<OrderLineForCreation> items, string idempotencyKey, string accessToken, CancellationToken ct = default)
        {
            Called = true; UserId = userId; Items = items; Key = idempotencyKey; AccessToken = accessToken;
            return Task.FromResult(result);
        }
    }
}
