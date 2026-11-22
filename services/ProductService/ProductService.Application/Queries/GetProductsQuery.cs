using MediatR;
using ProductService.Application.Common;
using ProductService.Domain.Entities;
using ProductService.Domain.Repositories;

namespace ProductService.Application.Queries.GetProducts;

public sealed record GetProductsQuery(string? Cursor, int Limit, string? Category) : IRequest<CursorPage<Product>>;

public sealed class GetProductsQueryHandler(IProductRepository repo) : IRequestHandler<GetProductsQuery, CursorPage<Product>>
{
    public async Task<CursorPage<Product>> Handle(GetProductsQuery query, CancellationToken ct)
    {
        var limit = query.Limit is <= 0 or > 100 ? 20 : query.Limit; // hard ceiling — never let a client force an unbounded scan
        var result = await repo.GetByCursorAsync(query.Cursor, limit, query.Category, ct);
        return new CursorPage<Product>(result.Items, result.NextCursor, result.HasMore);
    }
}
