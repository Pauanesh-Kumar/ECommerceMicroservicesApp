using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using OrderService.Application.Interfaces;
using OrderService.Contracts;
using OrderService.Domain.Entities;
using OrderService.Domain.Repositories;
using System.Text.Json;

namespace OrderService.Application.Commands;

public sealed record CreateOrderLine(Guid ProductId, string Name, decimal UnitPrice, int Quantity);
public sealed record CreateOrderCommand(Guid UserId, List<CreateOrderLine> Items, string IdempotencyKey) : IRequest<CreateOrderResult>;
public sealed record CreateOrderResult(Guid OrderId, bool WasAlreadyProcessed);

public sealed class CreateOrderCommandValidator : AbstractValidator<CreateOrderCommand>
{
    public CreateOrderCommandValidator()
    {
        RuleFor(x => x.Items).NotEmpty();
        RuleFor(x => x.IdempotencyKey).NotEmpty();
        RuleForEach(x => x.Items).ChildRules(item =>
        {
            item.RuleFor(i => i.Quantity).GreaterThan(0);
            item.RuleFor(i => i.UnitPrice).GreaterThan(0);
        });
    }
}

/// <summary>
/// Writes the Order + its Outbox message (order-created) + IdempotencyRecord in ONE
/// SaveChangesAsync call — the transactional-outbox guarantee. Application layer talks
/// only to IOrderUnitOfWork (an abstraction over the outbox/idempotency persistence
/// concerns) so it never references EF Core / OrderDbContext directly — that stays an
/// Infrastructure concern, same DIP boundary UserService already applies via IUnitOfWork.
/// </summary>
public sealed class CreateOrderCommandHandler(
    IOrderUnitOfWork uow, IOrderRepository repo, ILogger<CreateOrderCommandHandler> logger)
    : IRequestHandler<CreateOrderCommand, CreateOrderResult>
{
    private static readonly JsonSerializerOptions Json = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    public async Task<CreateOrderResult> Handle(CreateOrderCommand cmd, CancellationToken ct)
    {
        var existingOrderId = await uow.FindOrderIdByIdempotencyKeyAsync(cmd.IdempotencyKey, ct);
        if (existingOrderId is { } id)
        {
            logger.LogInformation("Idempotent replay for key={IdempotencyKey} → OrderId={OrderId}", cmd.IdempotencyKey, id);
            return new CreateOrderResult(id, true);
        }

        var order = Order.Create(cmd.UserId, cmd.Items.Select(i => OrderItem.Create(i.ProductId, i.Name, i.UnitPrice, i.Quantity)));
        await repo.AddAsync(order, ct);
        uow.RecordIdempotencyKey(cmd.IdempotencyKey, order.Id);

        var evt = new OrderCreatedIntegrationEvent(
            order.Id, order.UserId,
            [.. order.Items.Select(i => new OrderCreatedLineItem(i.ProductId, i.Quantity))],
            DateTime.UtcNow);

        uow.EnqueueOutboxMessage("order-created", order.Id.ToString(), JsonSerializer.Serialize(evt, Json));

        try
        {
            await uow.SaveChangesAsync(ct); // Order + IdempotencyRecord + Outbox row — one transaction
        }
        catch (DbUpdateException)
        {
            // Check for concurrent duplicate request race condition
            var winnerId = await uow.FindOrderIdByIdempotencyKeyAsync(cmd.IdempotencyKey, ct);
            if (winnerId is { } id2)
            {
                // Lost the race to a concurrent duplicate request — return the winner's OrderId
                logger.LogInformation("Concurrency conflict detected for key={IdempotencyKey}, winner OrderId={WinnerId}", cmd.IdempotencyKey, id2);
                return new CreateOrderResult(id2, true);
            }
            throw; // Re-throw if not a duplicate
        }

        logger.LogInformation("Order created Id={OrderId} UserId={UserId} Total={Total}", order.Id, order.UserId, order.TotalAmount);
        return new CreateOrderResult(order.Id, false);
    }
}