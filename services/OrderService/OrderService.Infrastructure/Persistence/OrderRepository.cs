using Microsoft.EntityFrameworkCore;
using OrderService.Domain.Common;
using OrderService.Domain.Entities;
using OrderService.Domain.Repositories;

namespace OrderService.Infrastructure.Persistence;

public sealed class OrderRepository(OrderDbContext db) : IOrderRepository
{
    public Task<Order?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => db.Orders.Include(o => o.Items).AsTracking().FirstOrDefaultAsync(o => o.Id == id, ct);

    public async Task AddAsync(Order order, CancellationToken ct = default)
        => await db.Orders.AddAsync(order, ct);

    /// <summary>
    /// Keyset pagination via the composite (CreatedAtUtc, Id) seek predicate — same
    /// approach as ProductService.Infrastructure.Persistence.ProductRepository, translated
    /// to LINQ/EF Core. Backed by ix_orders_cursor (see OrderDbContext). Fetches limit+1
    /// rows to detect HasMore without a separate COUNT query.
    /// </summary>
    public async Task<CursorPageResult<Order>> GetByCursorAsync(
        string? cursor, int limit, Guid? userId, CancellationToken ct = default)
    {
        var query = db.Orders.Include(o => o.Items).AsNoTracking().AsQueryable();

        if (userId is { } uid)
            query = query.Where(o => o.UserId == uid);

        var decoded = CursorEncoder.Decode(cursor);
        if (decoded is { } key)
        {
            query = query.Where(o =>
                o.CreatedAtUtc > key.CreatedAtUtc ||
                (o.CreatedAtUtc == key.CreatedAtUtc && o.Id.CompareTo(key.Id) > 0));
        }

        var rows = await query
            .OrderBy(o => o.CreatedAtUtc).ThenBy(o => o.Id)
            .Take(limit + 1)
            .ToListAsync(ct);

        var hasMore = rows.Count > limit;
        var page = hasMore ? rows.Take(limit).ToList() : rows;
        var nextCursor = page.Count > 0 ? CursorEncoder.Encode(page[^1].CreatedAtUtc, page[^1].Id) : null;

        return new CursorPageResult<Order>(page, hasMore ? nextCursor : null, hasMore);
    }
}
