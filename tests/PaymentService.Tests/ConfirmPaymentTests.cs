using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using PaymentService.Application.Commands.ConfirmPayment;
using PaymentService.Application.Interfaces;
using PaymentService.Domain.Entities;

namespace PaymentService.Tests;

public class ConfirmPaymentTests
{
    [Theory]
    [InlineData(true, PaymentStatus.Succeeded, "PaymentProcessed")]
    [InlineData(false, PaymentStatus.Failed, "PaymentFailed")]
    public async Task RecordsWebhookAndOutboxWithPaymentInSingleSave(bool success, PaymentStatus expectedStatus, string expectedEvent)
    {
        var payment = Payment.CreatePending(Guid.NewGuid(), Guid.NewGuid(), 25m, "INR", "provider-order");
        var uow = new FakeUnitOfWork(payment);
        var handler = new ConfirmPaymentCommandHandler(uow, NullLogger<ConfirmPaymentCommandHandler>.Instance);

        await handler.Handle(new("event-123", "provider-order", "provider-payment", success, "declined"), default);

        Assert.Equal(expectedStatus, payment.Status);
        Assert.Equal("event-123", uow.InboxId);
        Assert.Equal("payment-status", uow.Topic);
        Assert.Equal(payment.OrderId.ToString(), uow.Key);
        Assert.Equal(1, uow.Saves);
        using var envelope = JsonDocument.Parse(Assert.IsType<string>(uow.Payload));
        Assert.Equal(expectedEvent, envelope.RootElement.GetProperty("eventType").GetString());
        using var eventPayload = JsonDocument.Parse(envelope.RootElement.GetProperty("payload").GetString()!);
        Assert.Equal(payment.OrderId, eventPayload.RootElement.GetProperty("orderId").GetGuid());
    }

    [Fact]
    public async Task UnknownProviderOrderFailsWithoutRecordingInbox()
    {
        var uow = new FakeUnitOfWork(null);
        var handler = new ConfirmPaymentCommandHandler(uow, NullLogger<ConfirmPaymentCommandHandler>.Instance);
        await Assert.ThrowsAsync<InvalidOperationException>(() => handler.Handle(new("event-123", "missing", "payment", true, null), default));
        Assert.Null(uow.InboxId);
        Assert.Equal(0, uow.Saves);
    }

    [Fact]
    public async Task ResolvedPaymentDoesNotPublishTwice()
    {
        var payment = Payment.CreatePending(Guid.NewGuid(), Guid.NewGuid(), 25m, "INR", "provider-order");
        payment.MarkSucceeded("provider-payment");
        var uow = new FakeUnitOfWork(payment);
        var handler = new ConfirmPaymentCommandHandler(uow, NullLogger<ConfirmPaymentCommandHandler>.Instance);
        await handler.Handle(new("event-123", "provider-order", "provider-payment", true, null), default);
        Assert.Null(uow.Topic);
        Assert.Equal(0, uow.Saves);
    }

    private sealed class FakeUnitOfWork(Payment? payment) : IPaymentUnitOfWork
    {
        public string? InboxId { get; private set; }
        public string? Topic { get; private set; }
        public string? Key { get; private set; }
        public string? Payload { get; private set; }
        public int Saves { get; private set; }
        public Task<Payment?> FindByProviderOrderIdAsync(string id, CancellationToken ct = default) => Task.FromResult(payment);
        public Task<Payment?> FindByOrderIdAsync(Guid id, CancellationToken ct = default) => throw new NotSupportedException();
        public void Add(Payment value) => throw new NotSupportedException();
        public void EnqueueOutboxMessage(string topic, string key, string payload) { Topic = topic; Key = key; Payload = payload; }
        public void RecordInboxEvent(string eventId) => InboxId = eventId;
        public Task<bool> TrySaveChangesAsync(CancellationToken ct = default) { Saves++; return Task.FromResult(true); }
    }
}
