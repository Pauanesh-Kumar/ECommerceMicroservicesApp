using MediatR;
using OrderService.Domain.Repositories;
using OrderService.Domain.Entities;

namespace OrderService.Application.Queries.GetOrders;

public sealed record GetOrdersQuery(string? Cursor, int Limit, Guid UserId)
    : IRequest<GetOrdersResult>;

public sealed record GetOrdersResult(
    IReadOnlyList<OrderDto> Orders,
    string? NextCursor,
    bool HasMore,
    int TotalCount);

public sealed record OrderDto(
    Guid OrderId,
    Guid UserId,
    decimal Total,
    DateTime CreatedAtUtc,
    string Status);

public class GetOrdersQueryHandler : IRequestHandler<GetOrdersQuery, GetOrdersResult>
{
    private readonly IOrderRepository _repository;

    public GetOrdersQueryHandler(IOrderRepository repository)
    {
        _repository = repository;
    }

    public async Task<GetOrdersResult> Handle(GetOrdersQuery query, CancellationToken ct)
    {
        // Use the existing GetByCursorAsync method with keyset pagination
        var pageResult = await _repository.GetByCursorAsync(
            query.Cursor,
            query.Limit + 1, // +1 to detect if more results exist
            query.UserId,
            ct);

        // Determine if there are more results
        var hasMore = pageResult.Items.Count > query.Limit;
        var items = hasMore
            ? pageResult.Items.Take(query.Limit).ToList()
            : pageResult.Items;

        // Map to DTOs using correct property names
        var orderDtos = items
            .Select(o => new OrderDto(
                o.Id,
                o.UserId,
                o.TotalAmount,
                o.CreatedAtUtc,  
                o.Status.ToString()))
            .ToList();

        // Return result with cursor pagination metadata
        // Note: TotalCount is 0 because cursor pagination doesn't typically include total count
        // This is by design - cursor pagination is efficient for streaming large datasets
        return new GetOrdersResult(
            orderDtos,
            pageResult.NextCursor,
            pageResult.HasMore,
            0); // Total count not available with cursor pagination (intentional)
    }
}