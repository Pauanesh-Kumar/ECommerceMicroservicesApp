using MediatR;
using Microsoft.Extensions.Logging;
using OrderService.Application.Interfaces;
using OrderService.Contracts;
using OrderService.Domain.Entities;
using OrderService.Domain.Repositories;
using System.Text.Json;

namespace OrderService.Application.Commands;

// ── AwaitingPayment: stock reserved, ask Payment to charge ──────────────────
public sealed record MarkAwaitingPaymentCommand(Guid OrderId) : IRequest;

public sealed class MarkAwaitingPaymentCommandHandler(
    IOrderRepository repo, IOrderUnitOfWork uow, ILogger<MarkAwaitingPaymentCommandHandler> logger)
    : IRequestHandler<MarkAwaitingPaymentCommand>
{
    private static readonly JsonSerializerOptions Json = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    public async Task Handle(MarkAwaitingPaymentCommand cmd, CancellationToken ct)
    {
        var order = await repo.GetByIdAsync(cmd.OrderId, ct);
        if (order is null) { logger.LogWarning("Order {OrderId} not found", cmd.OrderId); return; }
        if (order.Status != OrderStatus.Pending) return; // already progressed — Inbox should prevent this, belt & braces

        order.MarkAwaitingPayment();

        uow.EnqueueOutboxMessage("payment-requested", order.Id.ToString(), JsonSerializer.Serialize(
            new PaymentRequestedIntegrationEvent(order.Id, order.UserId, order.TotalAmount, order.Currency, DateTime.UtcNow), Json));

        await uow.SaveChangesAsync(ct);
    }
}

// ── Confirmed: payment succeeded ─────────────────────────────────────────────
public sealed record ConfirmOrderCommand(Guid OrderId, Guid PaymentId) : IRequest;

public sealed class ConfirmOrderCommandHandler(
    IOrderRepository repo, IOrderUnitOfWork uow, ILogger<ConfirmOrderCommandHandler> logger)
    : IRequestHandler<ConfirmOrderCommand>
{
    private static readonly JsonSerializerOptions Json = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    public async Task Handle(ConfirmOrderCommand cmd, CancellationToken ct)
    {
        var order = await repo.GetByIdAsync(cmd.OrderId, ct);
        if (order is null) return;
        if (order.Status == OrderStatus.Confirmed) return; // already handled (Inbox should prevent this, belt & braces)

        order.Confirm();

        uow.EnqueueOutboxMessage("order-confirmed", order.Id.ToString(), JsonSerializer.Serialize(
            new OrderConfirmedIntegrationEvent(order.Id, order.UserId, order.TotalAmount, DateTime.UtcNow), Json));

        await uow.SaveChangesAsync(ct);
        logger.LogInformation("Order {OrderId} confirmed via PaymentId={PaymentId}", order.Id, cmd.PaymentId);
    }
}

// ── Cancelled: stock unavailable OR payment failed (with compensation) ──────
public sealed record CancelOrderCommand(Guid OrderId, string Reason, bool ReleaseStock) : IRequest;

public sealed class CancelOrderCommandHandler(
    IOrderRepository repo, IOrderUnitOfWork uow, ILogger<CancelOrderCommandHandler> logger)
    : IRequestHandler<CancelOrderCommand>
{
    private static readonly JsonSerializerOptions Json = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    public async Task Handle(CancelOrderCommand cmd, CancellationToken ct)
    {
        var order = await repo.GetByIdAsync(cmd.OrderId, ct);
        if (order is null) return;
        if (order.Status == OrderStatus.Cancelled) return;

        order.Cancel(cmd.Reason);

        uow.EnqueueOutboxMessage("order-cancelled", order.Id.ToString(), JsonSerializer.Serialize(
            new OrderCancelledIntegrationEvent(order.Id, order.UserId, cmd.Reason, DateTime.UtcNow), Json));

        // Compensation: only needed if stock was actually reserved before the failure
        // (payment failed AFTER StockReserved — not if stock reservation itself failed).
        if (cmd.ReleaseStock)
        {
            uow.EnqueueOutboxMessage("stock-release", order.Id.ToString(), JsonSerializer.Serialize(
                new StockReleaseRequestedEvent(
                    order.Id,
                    order.Items.Select(i => new OrderCreatedLineItem(i.ProductId, i.Quantity)).ToList(),
                    DateTime.UtcNow), Json));
        }

        await uow.SaveChangesAsync(ct);
        logger.LogWarning("Order {OrderId} cancelled. Reason={Reason} ReleaseStock={ReleaseStock}", order.Id, cmd.Reason, cmd.ReleaseStock);
    }
}
