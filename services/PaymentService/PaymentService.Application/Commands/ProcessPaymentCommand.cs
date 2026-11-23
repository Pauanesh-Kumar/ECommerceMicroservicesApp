using MediatR;
using Microsoft.Extensions.Logging;
using PaymentService.Application.Interfaces;
using PaymentService.Domain.Entities;

namespace PaymentService.Application.Commands.ProcessPayment;

/// <summary>Starts a provider order for a durable payment request. A signed webhook
/// delivers the eventual payment outcome.</summary>
public sealed record ProcessPaymentCommand(Guid OrderId, Guid UserId, decimal Amount, string Currency, string EventId) : IRequest;

public sealed class ProcessPaymentCommandHandler(
    IPaymentUnitOfWork uow, IPaymentGateway gateway, ILogger<ProcessPaymentCommandHandler> logger) : IRequestHandler<ProcessPaymentCommand>
{
    public async Task Handle(ProcessPaymentCommand cmd, CancellationToken ct)
    {
        var existing = await uow.FindByOrderIdAsync(cmd.OrderId, ct);
        if (existing is not null)
        {
            if (existing.UserId != cmd.UserId || existing.Amount != cmd.Amount || existing.Currency != cmd.Currency)
                throw new InvalidOperationException("Payment request conflicts with the existing order.");

            if (existing.Status == PaymentStatus.Initiating)
                logger.LogError("Payment for OrderId={OrderId} is still initiating; provider reconciliation is required", cmd.OrderId);
            else
                logger.LogInformation("Payment already exists for OrderId={OrderId} (Status={Status})", cmd.OrderId, existing.Status);
            return;
        }

        var payment = Payment.CreateInitiating(cmd.OrderId, cmd.UserId, cmd.Amount, cmd.Currency);
        uow.Add(payment);
        uow.RecordInboxEvent(cmd.EventId);
        if (!await uow.TrySaveChangesAsync(ct))
            throw new InvalidOperationException("Could not persist payment intent and inbox; retry the event.");

        var gatewayResult = await gateway.CreateOrderAsync(cmd.OrderId, cmd.Amount, cmd.Currency, ct);
        if (!gatewayResult.Success || string.IsNullOrWhiteSpace(gatewayResult.ProviderOrderId))
        {
            logger.LogError("Provider order outcome is unknown for OrderId={OrderId}; reconciliation is required", cmd.OrderId);
            return;
        }

        payment.SetProviderOrder(gatewayResult.ProviderOrderId);
        if (!await uow.TrySaveChangesAsync(ct))
            throw new InvalidOperationException("Could not persist provider order; reconciliation is required.");

        logger.LogInformation("Razorpay order {ProviderOrderId} created for OrderId={OrderId}, awaiting client checkout/webhook",
            gatewayResult.ProviderOrderId, cmd.OrderId);
    }
}
