using Microsoft.Extensions.Logging.Abstractions;
using PaymentService.Application.Commands.ProcessPayment;
using PaymentService.Application.Interfaces;
using PaymentService.Domain.Entities;

namespace PaymentService.Tests;

public class PaymentTests
{
    private static ProcessPaymentCommand Command() => new(Guid.NewGuid(), Guid.NewGuid(), 50m, "INR", Guid.NewGuid().ToString());

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ExistingPaymentDoesNotCallProviderAgain(bool initiating)
    {
        var command = Command();
        var uow = new FakeUnitOfWork { Existing = initiating
            ? Payment.CreateInitiating(command.OrderId, command.UserId, command.Amount, command.Currency)
            : Payment.CreatePending(command.OrderId, command.UserId, command.Amount, command.Currency, "provider") };
        var gateway = new FakeGateway(uow, new GatewayOrderResult(true, "provider", null));
        await new ProcessPaymentCommandHandler(uow, gateway, NullLogger<ProcessPaymentCommandHandler>.Instance).Handle(command, default);
        Assert.Equal(0, gateway.Calls);
        Assert.Equal(0, uow.Saves);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task PersistsIntentAndInboxBeforeProviderCall(bool success)
    {
        var command = Command();
        var uow = new FakeUnitOfWork();
        var gateway = new FakeGateway(uow, new GatewayOrderResult(success, success ? "provider" : null, "unknown"));
        await new ProcessPaymentCommandHandler(uow, gateway, NullLogger<ProcessPaymentCommandHandler>.Instance).Handle(command, default);
        Assert.Equal(1, gateway.Calls);
        Assert.Equal(command.EventId, uow.InboxId);
        Assert.Equal(success ? 2 : 1, uow.Saves);
        Assert.Equal(success ? PaymentStatus.Pending : PaymentStatus.Initiating, uow.Added!.Status);
        Assert.Null(uow.Topic);
    }

    [Fact]
    public async Task FailedIntentSaveDoesNotContactProvider()
    {
        var uow = new FakeUnitOfWork { SaveResult = false };
        var gateway = new FakeGateway(uow, new GatewayOrderResult(true, "provider", null));
        await Assert.ThrowsAsync<InvalidOperationException>(() => new ProcessPaymentCommandHandler(uow, gateway, NullLogger<ProcessPaymentCommandHandler>.Instance).Handle(Command(), default));
        Assert.Equal(0, gateway.Calls);
    }

    [Fact]
    public async Task ConflictingDuplicateIsNotAcknowledged()
    {
        var command = Command();
        var uow = new FakeUnitOfWork { Existing = Payment.CreateInitiating(command.OrderId, command.UserId, 10m, command.Currency) };
        var gateway = new FakeGateway(uow, new GatewayOrderResult(true, "provider", null));
        await Assert.ThrowsAsync<InvalidOperationException>(() => new ProcessPaymentCommandHandler(uow, gateway, NullLogger<ProcessPaymentCommandHandler>.Instance).Handle(command, default));
        Assert.Equal(0, gateway.Calls);
    }

    private sealed class FakeGateway(FakeUnitOfWork uow, GatewayOrderResult result) : IPaymentGateway
    {
        public int Calls { get; private set; }
        public Task<GatewayOrderResult> CreateOrderAsync(Guid orderId, decimal amount, string currency, CancellationToken ct = default)
        {
            Assert.Equal(1, uow.Saves);
            Assert.NotNull(uow.InboxId);
            Assert.Equal(PaymentStatus.Initiating, uow.Added!.Status);
            Calls++;
            return Task.FromResult(result);
        }
        public bool VerifySignature(string payload, string signatureHeader) => throw new NotSupportedException();
    }

    private sealed class FakeUnitOfWork : IPaymentUnitOfWork
    {
        public Payment? Existing { get; init; }
        public Payment? Added { get; private set; }
        public string? InboxId { get; private set; }
        public string? Topic { get; private set; }
        public int Saves { get; private set; }
        public bool SaveResult { get; init; } = true;
        public Task<Payment?> FindByOrderIdAsync(Guid orderId, CancellationToken ct = default) => Task.FromResult(Existing);
        public Task<Payment?> FindByProviderOrderIdAsync(string id, CancellationToken ct = default) => throw new NotSupportedException();
        public void Add(Payment payment) => Added = payment;
        public void EnqueueOutboxMessage(string topic, string key, string payload) => Topic = topic;
        public void RecordInboxEvent(string eventId) => InboxId = eventId;
        public Task<bool> TrySaveChangesAsync(CancellationToken ct = default) { Saves++; return Task.FromResult(SaveResult); }
    }
}
