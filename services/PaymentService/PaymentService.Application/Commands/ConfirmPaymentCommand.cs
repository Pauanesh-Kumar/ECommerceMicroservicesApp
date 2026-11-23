using MediatR;
using Microsoft.Extensions.Logging;
using PaymentService.Application.Interfaces;
using PaymentService.Contracts;
using PaymentService.Domain.Entities;
using System.Text.Json;

namespace PaymentService.Application.Commands.ConfirmPayment;

/// <summary>Handles a verified provider webhook or an authenticated provider reconciliation result.</summary>
public sealed record ConfirmPaymentCommand(string EventId, string ProviderOrderId, string ProviderPaymentId, bool Success, string? FailureReason) : IRequest;

public sealed class ConfirmPaymentCommandHandler(
    IPaymentUnitOfWork uow, ILogger<ConfirmPaymentCommandHandler> logger) : IRequestHandler<ConfirmPaymentCommand>
{
    private static readonly JsonSerializerOptions Json = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    public async Task Handle(ConfirmPaymentCommand cmd, CancellationToken ct)
    {
        var payment = await uow.FindByProviderOrderIdAsync(cmd.ProviderOrderId, ct);
        if (payment is null)
        {
            logger.LogWarning("Webhook referenced unknown ProviderOrderId={ProviderOrderId}", cmd.ProviderOrderId);
            throw new InvalidOperationException("Webhook references an unknown provider order.");
        }

        if (!cmd.Success)
        {
            uow.RecordInboxEvent(cmd.EventId);
            await uow.TrySaveChangesAsync(ct);
            logger.LogInformation("Payment attempt {ProviderPaymentId} failed for payment {PaymentId}; the provider order may still be paid", cmd.ProviderPaymentId, payment.Id);
            return;
        }

        if (payment.Status != PaymentStatus.Pending)
        {
            if (payment.Status != PaymentStatus.Succeeded || payment.ProviderPaymentId != cmd.ProviderPaymentId)
            {
                logger.LogError("Conflicting webhook {EventId} for payment {PaymentId} in status {Status}; reconciliation required", cmd.EventId, payment.Id, payment.Status);
                throw new InvalidOperationException("Webhook conflicts with the resolved payment; reconciliation is required.");
            }

            logger.LogInformation("Payment {PaymentId} already resolved (Status={Status}) — matching webhook ignored", payment.Id, payment.Status);
            return;
        }

        payment.MarkSucceeded(cmd.ProviderPaymentId);
        var evt = new PaymentProcessedIntegrationEvent(payment.OrderId, payment.Id, cmd.ProviderPaymentId, DateTime.UtcNow);
        const string eventType = "PaymentProcessed";
        uow.EnqueueOutboxMessage("payment-status", payment.OrderId.ToString(),
            JsonSerializer.Serialize(new EventEnvelope(eventType, JsonSerializer.Serialize(evt, Json)), Json));
        uow.RecordInboxEvent(cmd.EventId);

        if (!await uow.TrySaveChangesAsync(ct))
        {
            logger.LogInformation("Webhook event {EventId} was already processed", cmd.EventId);
            return;
        }
        logger.LogInformation("Payment {PaymentId} resolved: {EventType}", payment.Id, eventType);
    }
}
