using MediatR;
using Microsoft.Extensions.Logging;
using PaymentService.Application.Interfaces;
using PaymentService.Contracts;
using PaymentService.Domain.Entities;
using System.Text.Json;

namespace PaymentService.Application.Commands.ProcessPayment;

/// <summary>Triggered by "payment-requested". Creates a Razorpay order and a local
/// Pending Payment row — the actual success/failure arrives later via the Razorpay
/// webhook (ConfirmPaymentCommand), NOT synchronously here, because Razorpay Checkout
/// is a client-side redirect/modal flow. This command's job is only "start the charge".</summary>
public sealed record ProcessPaymentCommand(Guid OrderId, Guid UserId, decimal Amount, string Currency) : IRequest;

public sealed class ProcessPaymentCommandHandler(
    IPaymentUnitOfWork uow, IPaymentGateway gateway, ILogger<ProcessPaymentCommandHandler> logger) : IRequestHandler<ProcessPaymentCommand>
{
    private static readonly JsonSerializerOptions Json = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    public async Task Handle(ProcessPaymentCommand cmd, CancellationToken ct)
    {
        // Idempotency guard #1 — fast path, avoids a wasted Razorpay API call on redelivery.
        var existing = await uow.FindByOrderIdAsync(cmd.OrderId, ct);
        if (existing is not null)
        {
            logger.LogInformation("Payment already exists for OrderId={OrderId} (Status={Status}) — skipping re-charge", cmd.OrderId, existing.Status);
            return;
        }

        var gatewayResult = await gateway.CreateOrderAsync(cmd.OrderId, cmd.Amount, cmd.Currency, ct);
        if (!gatewayResult.Success)
        {
            uow.EnqueueOutboxMessage("payment-status", cmd.OrderId.ToString(), JsonSerializer.Serialize(
                new EventEnvelope("PaymentFailed", JsonSerializer.Serialize(
                    new PaymentFailedIntegrationEvent(cmd.OrderId, null, gatewayResult.Error ?? "Gateway order creation failed", DateTime.UtcNow), Json)), Json));
            await uow.TrySaveChangesAsync(ct);
            return;
        }

        var payment = Payment.CreatePending(cmd.OrderId, cmd.UserId, cmd.Amount, cmd.Currency, gatewayResult.ProviderOrderId!);
        uow.Add(payment);

        // Idempotency guard #2 — the DB-level guard (unique index on OrderId). If a
        // concurrent duplicate beat us here, this returns false and we no-op.
        var saved = await uow.TrySaveChangesAsync(ct);
        if (!saved)
        {
            logger.LogInformation("Lost race creating Payment for OrderId={OrderId} — another instance won", cmd.OrderId);
            return;
        }

        logger.LogInformation("Razorpay order {ProviderOrderId} created for OrderId={OrderId}, awaiting client checkout/webhook",
            gatewayResult.ProviderOrderId, cmd.OrderId);
    }
}
