using MediatR;
using Microsoft.Extensions.Logging;
using PaymentService.Application.Interfaces;
using PaymentService.Contracts;
using PaymentService.Domain.Entities;
using System.Text.Json;

namespace PaymentService.Application.Commands.ConfirmPayment;

/// <summary>Called from the Razorpay webhook endpoint AFTER signature verification.
/// This is the ONLY place a Payment transitions to Succeeded/Failed — never trust a
/// client-side "it worked" callback without this server-to-server confirmation.</summary>
public sealed record ConfirmPaymentCommand(string ProviderOrderId, string ProviderPaymentId, bool Success, string? FailureReason) : IRequest;

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
            return;
        }

        if (payment.Status != PaymentStatus.Pending)
        {
            logger.LogInformation("Payment {PaymentId} already resolved (Status={Status}) — webhook is a duplicate, ignoring", payment.Id, payment.Status);
            return;
        }

        string eventType;
        object evt;

        if (cmd.Success)
        {
            payment.MarkSucceeded(cmd.ProviderPaymentId);
            eventType = "PaymentProcessed";
            evt = new PaymentProcessedIntegrationEvent(payment.OrderId, payment.Id, cmd.ProviderPaymentId, DateTime.UtcNow);
        }
        else
        {
            payment.MarkFailed(cmd.FailureReason ?? "Payment declined");
            eventType = "PaymentFailed";
            evt = new PaymentFailedIntegrationEvent(payment.OrderId, payment.Id, cmd.FailureReason ?? "Payment declined", DateTime.UtcNow);
        }

        uow.EnqueueOutboxMessage("payment-status", payment.OrderId.ToString(),
            JsonSerializer.Serialize(new EventEnvelope(eventType, JsonSerializer.Serialize(evt, Json)), Json));

        await uow.TrySaveChangesAsync(ct);
        logger.LogInformation("Payment {PaymentId} resolved: {EventType}", payment.Id, eventType);
    }
}
