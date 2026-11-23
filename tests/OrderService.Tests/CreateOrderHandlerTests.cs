using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using OrderService.Application.Commands;
using OrderService.Application.Interfaces;
using OrderService.Domain.Entities;
using OrderService.Domain.Repositories;

namespace OrderService.Tests;

public class CreateOrderHandlerTests
{
    [Fact]
    public async Task NewOrderRecordsIdempotencyAndOutboxBeforeOneSave()
    {
        var repo = new FakeRepository();
        var uow = new FakeUnitOfWork();
        var productId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var handler = new CreateOrderCommandHandler(uow, repo, NullLogger<CreateOrderCommandHandler>.Instance);

        var result = await handler.Handle(new(userId, [new(productId, "Item", 12m, 2)], "key"), default);

        Assert.False(result.WasAlreadyProcessed);
        Assert.NotNull(repo.Order);
        Assert.Equal(result.OrderId, repo.Order.Id);
        Assert.Equal(24m, repo.Order.TotalAmount);
        Assert.Equal("key", uow.Key);
        Assert.Equal(result.OrderId, uow.RecordedOrderId);
        Assert.Equal("order-created", uow.Topic);
        Assert.Equal(result.OrderId.ToString(), uow.MessageKey);
        Assert.Equal(1, uow.Saves);
        using var payload = JsonDocument.Parse(Assert.IsType<string>(uow.Payload));
        Assert.Equal(result.OrderId, payload.RootElement.GetProperty("orderId").GetGuid());
        Assert.Equal(userId, payload.RootElement.GetProperty("userId").GetGuid());
        Assert.Equal(productId, payload.RootElement.GetProperty("items")[0].GetProperty("productId").GetGuid());
        Assert.Equal(2, payload.RootElement.GetProperty("items")[0].GetProperty("quantity").GetInt32());
    }

    [Fact]
    public async Task ReplayedRequestReturnsExistingOrderWithoutWriting()
    {
        var existingId = Guid.NewGuid();
        var repo = new FakeRepository();
        var uow = new FakeUnitOfWork { ExistingId = existingId };
        var handler = new CreateOrderCommandHandler(uow, repo, NullLogger<CreateOrderCommandHandler>.Instance);

        var result = await handler.Handle(new(Guid.NewGuid(), [new(Guid.NewGuid(), "Item", 1m, 1)], "key"), default);

        Assert.True(result.WasAlreadyProcessed);
        Assert.Equal(existingId, result.OrderId);
        Assert.Null(repo.Order);
        Assert.Null(uow.Topic);
        Assert.Equal(0, uow.Saves);
    }

    private sealed class FakeRepository : IOrderRepository
    {
        public Order? Order { get; private set; }
        public Task AddAsync(Order order, CancellationToken ct = default) { Order = order; return Task.CompletedTask; }
        public Task<Order?> GetByIdAsync(Guid id, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<CursorPageResult<Order>> GetByCursorAsync(string? cursor, int limit, Guid? userId, CancellationToken ct = default) => throw new NotSupportedException();
    }

    private sealed class FakeUnitOfWork : IOrderUnitOfWork
    {
        public Guid? ExistingId { get; init; }
        public string? Key { get; private set; }
        public Guid RecordedOrderId { get; private set; }
        public string? Topic { get; private set; }
        public string? MessageKey { get; private set; }
        public string? Payload { get; private set; }
        public int Saves { get; private set; }
        public Task<Guid?> FindOrderIdByIdempotencyKeyAsync(string key, CancellationToken ct = default) => Task.FromResult(ExistingId);
        public void RecordIdempotencyKey(string key, Guid orderId) { Key = key; RecordedOrderId = orderId; }
        public void EnqueueOutboxMessage(string topic, string key, string payload) { Topic = topic; MessageKey = key; Payload = payload; }
        public Task SaveChangesAsync(CancellationToken ct = default) { Saves++; return Task.CompletedTask; }
    }
}
