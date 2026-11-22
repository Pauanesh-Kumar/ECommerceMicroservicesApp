using MediatR;
using Microsoft.Extensions.Logging;
using ProductService.Contracts;
using ProductService.Domain.Repositories;

namespace ProductService.Application.Commands.ReserveStock;

public sealed record ReserveStockCommand(Guid OrderId, IReadOnlyList<OrderCreatedLineItem> Items) : IRequest<ReserveStockResult>;

public sealed record ReserveStockResult(bool Success, IReadOnlyList<Guid> UnavailableProductIds, IReadOnlyList<Guid> ReservedProductIds);

/// <summary>
/// Saga participant. Reserves every line item atomically one-by-one; if any line fails
/// (insufficient stock), releases everything already reserved for this order — so the
/// operation is all-or-nothing from the caller's perspective even without a Mongo
/// multi-document transaction (single-node atomic ops per line + explicit local
/// compensation on partial failure).
/// </summary>
public sealed class ReserveStockCommandHandler(
    IProductRepository repo, ILogger<ReserveStockCommandHandler> logger) : IRequestHandler<ReserveStockCommand, ReserveStockResult>
{
    public async Task<ReserveStockResult> Handle(ReserveStockCommand cmd, CancellationToken ct)
    {
        var reserved = new List<Guid>();
        var unavailable = new List<Guid>();

        foreach (var item in cmd.Items)
        {
            var ok = await repo.TryReserveStockAsync(item.ProductId, item.Quantity, ct);
            if (ok) reserved.Add(item.ProductId);
            else unavailable.Add(item.ProductId);
        }

        if (unavailable.Count > 0)
        {
            foreach (var productId in reserved)
            {
                var qty = cmd.Items.First(i => i.ProductId == productId).Quantity;
                await repo.ReleaseStockAsync(productId, qty, ct);
            }
            logger.LogWarning("Stock reservation failed for OrderId={OrderId}. Unavailable={Unavailable}",
                cmd.OrderId, string.Join(",", unavailable));
            return new ReserveStockResult(false, unavailable, []);
        }

        logger.LogInformation("Stock reserved for OrderId={OrderId}", cmd.OrderId);
        return new ReserveStockResult(true, [], reserved);
    }
}
