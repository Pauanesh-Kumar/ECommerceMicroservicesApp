using MediatR;
using Microsoft.Extensions.Logging;
using ProductService.Contracts;
using ProductService.Domain.Repositories;

namespace ProductService.Application.Commands.ReleaseStock;

/// <summary>Compensating command — triggered by StockReleaseRequestedEvent when a
/// downstream saga step (payment) fails after stock was already reserved.</summary>
public sealed record ReleaseStockCommand(Guid OrderId, IReadOnlyList<OrderCreatedLineItem> Items) : IRequest;

public sealed class ReleaseStockCommandHandler(
    IProductRepository repo, ILogger<ReleaseStockCommandHandler> logger) : IRequestHandler<ReleaseStockCommand>
{
    public async Task Handle(ReleaseStockCommand cmd, CancellationToken ct)
    {
        foreach (var item in cmd.Items)
            await repo.ReleaseStockAsync(item.ProductId, item.Quantity, ct);

        logger.LogInformation("Stock released for OrderId={OrderId} (compensation)", cmd.OrderId);
    }
}
