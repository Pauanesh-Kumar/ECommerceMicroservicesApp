using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using OrderService.Application.Commands;
using OrderService.Application.Interfaces;
using OrderService.Domain.Entities;
using OrderService.Domain.Repositories;

namespace OrderService.Tests;

public class CustomerNotificationTests
{
    private static Order NewOrder(string? email = "jane@example.com", string? name = "Jane Q Doe", bool awaitingPayment = true)
    {
        var order = Order.Create(Guid.NewGuid(), [OrderItem.Create(Guid.NewGuid(), "Item", 10m, 3)], email, name);
        if (awaitingPayment) order.MarkAwaitingPayment();
        return order;
    }

    [Fact]
    public async Task CreateWithContactPublishesOrderPlacedInSameUnitOfWork()
    {
        var uow = new RecordingUow();
        var handler = new CreateOrderCommandHandler(uow, new Repo(null), NullLogger<CreateOrderCommandHandler>.Instance);

        await handler.Handle(new(Guid.NewGuid(), [new(Guid.NewGuid(), "Item", 10m, 3)], "k", "jane@example.com", "Jane Q Doe"), default);

        Assert.Equal(["order-created", "order-placed"], uow.Messages.Select(m => m.Topic));
        using var doc = JsonDocument.Parse(uow.Messages[1].Payload);
        var root = doc.RootElement;
        Assert.Equal("OrderPlaced", root.GetProperty("eventType").GetString());
        Assert.Equal("jane@example.com", root.GetProperty("email").GetString());
        Assert.Equal("Jane", root.GetProperty("firstName").GetString());
        Assert.Equal("Q Doe", root.GetProperty("lastName").GetString());
        Assert.Equal(30m, root.GetProperty("totalAmount").GetDecimal());
        Assert.Equal(1, uow.Saves);
    }

    [Fact]
    public async Task CreateWithoutEmailPublishesNoNotification()
    {
        var uow = new RecordingUow();
        var handler = new CreateOrderCommandHandler(uow, new Repo(null), NullLogger<CreateOrderCommandHandler>.Instance);

        await handler.Handle(new(Guid.NewGuid(), [new(Guid.NewGuid(), "Item", 10m, 1)], "k"), default);

        Assert.Equal(["order-created"], uow.Messages.Select(m => m.Topic));
    }

    [Fact]
    public async Task ConfirmPublishesPaymentProcessedSuccessNotification()
    {
        var order = NewOrder();
        var uow = new RecordingUow();
        var paymentId = Guid.NewGuid();

        await new ConfirmOrderCommandHandler(new Repo(order), uow, NullLogger<ConfirmOrderCommandHandler>.Instance)
            .Handle(new(order.Id, paymentId), default);

        Assert.Equal(["order-confirmed", "payment-processed"], uow.Messages.Select(m => m.Topic));
        using var doc = JsonDocument.Parse(uow.Messages[1].Payload);
        Assert.Equal("PaymentProcessed", doc.RootElement.GetProperty("eventType").GetString());
        Assert.True(doc.RootElement.GetProperty("success").GetBoolean());
        Assert.Equal(paymentId, doc.RootElement.GetProperty("paymentId").GetGuid());
        Assert.Equal(30m, doc.RootElement.GetProperty("amount").GetDecimal());
    }

    [Fact]
    public async Task PaymentFailedCancellationPublishesPaymentFailedNotification()
    {
        var order = NewOrder();
        var uow = new RecordingUow();

        await new CancelOrderCommandHandler(new Repo(order), uow, NullLogger<CancelOrderCommandHandler>.Instance)
            .Handle(new(order.Id, "card declined", ReleaseStock: true, PaymentFailed: true), default);

        Assert.Equal(["order-cancelled", "payment-processed", "stock-release"], uow.Messages.Select(m => m.Topic));
        using var doc = JsonDocument.Parse(uow.Messages[1].Payload);
        Assert.Equal("PaymentFailed", doc.RootElement.GetProperty("eventType").GetString());
        Assert.False(doc.RootElement.GetProperty("success").GetBoolean());
        Assert.Equal("card declined", doc.RootElement.GetProperty("failureReason").GetString());
    }

    [Fact]
    public async Task StockFailureCancellationDoesNotNotifyAsPaymentFailure()
    {
        var order = NewOrder(awaitingPayment: false);
        var uow = new RecordingUow();

        await new CancelOrderCommandHandler(new Repo(order), uow, NullLogger<CancelOrderCommandHandler>.Instance)
            .Handle(new(order.Id, "out of stock", ReleaseStock: false), default);

        Assert.Equal(["order-cancelled"], uow.Messages.Select(m => m.Topic));
    }

    private sealed class Repo(Order? order) : IOrderRepository
    {
        public Task AddAsync(Order o, CancellationToken ct = default) => Task.CompletedTask;
        public Task<Order?> GetByIdAsync(Guid id, CancellationToken ct = default) => Task.FromResult(order);
        public Task<CursorPageResult<Order>> GetByCursorAsync(string? cursor, int limit, Guid? userId, CancellationToken ct = default) => throw new NotSupportedException();
    }

    private sealed class RecordingUow : IOrderUnitOfWork
    {
        public List<(string Topic, string Key, string Payload)> Messages { get; } = [];
        public int Saves { get; private set; }
        public Task<Guid?> FindOrderIdByIdempotencyKeyAsync(string key, CancellationToken ct = default) => Task.FromResult<Guid?>(null);
        public void RecordIdempotencyKey(string key, Guid orderId) { }
        public void EnqueueOutboxMessage(string topic, string key, string payload) => Messages.Add((topic, key, payload));
        public Task SaveChangesAsync(CancellationToken ct = default) { Saves++; return Task.CompletedTask; }
    }
}
