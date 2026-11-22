using OrderService.Domain.Entities;

namespace OrderService.Domain.Repositories;

public sealed record CursorPageResult<T>(IReadOnlyList<T> Items, string? NextCursor, bool HasMore);

public interface IOrderRepository
{
    Task<Order?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task AddAsync(Order order, CancellationToken ct = default);

    /// <summary>Keyset/cursor pagination on (CreatedAtUtc, Id) — see
    /// Domain.Common.CursorEncoder. Optionally scoped to a single user's order history.</summary>
    Task<CursorPageResult<Order>> GetByCursorAsync(string? cursor, int limit, Guid? userId, CancellationToken ct = default);
}
