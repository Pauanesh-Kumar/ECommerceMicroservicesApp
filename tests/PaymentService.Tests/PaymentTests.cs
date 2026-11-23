using Microsoft.Extensions.Logging.Abstractions;
using PaymentService.Application.Commands.ProcessPayment;
using PaymentService.Application.Interfaces;
using PaymentService.Domain.Entities;

namespace PaymentService.Tests;

public class PaymentTests
{
    [Fact]
    public async Task ExistingPaymentDoesNotChargeAgain()
    {
        var command = new ProcessPaymentCommand(Guid.NewGuid(), Guid.NewGuid(), 50m, "INR");
        var uow = new FakeUnitOfWork { Existing = Payment.CreatePending(command.OrderId, command.UserId, command.Amount, command.Currency, "provider") };
        var gateway = new FakeGateway(new GatewayOrderResult(true, "provider", null));
        await new ProcessPaymentCommandHandler(uow, gateway, NullLogger<ProcessPaymentCommandHandler>.Instance).Handle(command, default);
        Assert.Equal(0, gateway.Calls);
        Assert.Equal(0, uow.Saves);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ProcessingPersistsPendingPaymentOrFailureEvent(bool success)
    {
        var command = new ProcessPaymentCommand(Guid.NewGuid(), Guid.NewGuid(), 50m, "INR");
        var uow = new FakeUnitOfWork();
        var gateway = new FakeGateway(new GatewayOrderResult(success, success ? "provider" : null, success ? null : "declined"));
        await new ProcessPaymentCommandHandler(uow, gateway, NullLogger<ProcessPaymentCommandHandler>.Instance).Handle(command, default);
        Assert.Equal(1, gateway.Calls);
        Assert.Equal(1, uow.Saves);
        if (success)
        {
            Assert.NotNull(uow.Added);
            Assert.Equal(PaymentStatus.Pending, uow.Added.Status);
            Assert.Equal(command.OrderId, uow.Added.OrderId);
            Assert.Null(uow.Topic);
        }
        else
        {
            Assert.Null(uow.Added);
            Assert.Equal("payment-status", uow.Topic);
            Assert.Contains("PaymentFailed", uow.Payload);
        }
    }

    private sealed class FakeGateway(GatewayOrderResult result) : IPaymentGateway
    {
        public int Calls { get; private set; }
        public Task<GatewayOrderResult> CreateOrderAsync(Guid orderId, decimal amount, string currency, CancellationToken ct = default) { Calls++; return Task.FromResult(result); }
        public bool VerifySignature(string payload, string signatureHeader) => throw new NotSupportedException();
    }

    private sealed class FakeUnitOfWork : IPaymentUnitOfWork
    {
        public Payment? Existing { get; init; }
        public Payment? Added { get; private set; }
        public string? Topic { get; private set; }
        public string? Payload { get; private set; }
        public int Saves { get; private set; }
        public Task<Payment?> FindByOrderIdAsync(Guid orderId, CancellationToken ct = default) => Task.FromResult(Existing);
        public Task<Payment?> FindByProviderOrderIdAsync(string id, CancellationToken ct = default) => throw new NotSupportedException();
        public void Add(Payment payment) => Added = payment;
        public void EnqueueOutboxMessage(string topic, string key, string payload) { Topic = topic; Payload = payload; }
        public void RecordInboxEvent(string eventId) => throw new NotSupportedException();
        public Task<bool> TrySaveChangesAsync(CancellationToken ct = default) { Saves++; return Task.FromResult(true); }
    }
}
