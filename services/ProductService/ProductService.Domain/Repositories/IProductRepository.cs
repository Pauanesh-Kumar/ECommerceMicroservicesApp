using ProductService.Domain.Entities;

namespace ProductService.Domain.Repositories;

public sealed record CursorPageResult<T>(IReadOnlyList<T> Items, string? NextCursor, bool HasMore);

public interface IProductRepository
{
    Task<Product?> GetByIdAsync(Guid id, CancellationToken ct = default);

    /// <summary>Keyset/cursor pagination — see Domain.Common.CursorEncoder for the
    /// (CreatedAtUtc, Id) seek-method rationale. Pass the previous page's NextCursor
    /// to fetch the next page; omit for the first page.</summary>
    Task<CursorPageResult<Product>> GetByCursorAsync(string? cursor, int limit, string? category, CancellationToken ct = default);

    Task AddAsync(Product product, CancellationToken ct = default);
    Task<bool> UpdateAsync(Product product, long expectedVersion, CancellationToken ct = default);

    /// <summary>Atomically reserves <paramref name="quantity"/> units: succeeds only if
    /// Stock >= quantity at the moment of the update (single round-trip, no read-then-write
    /// race). Returns false ("insufficient stock") without throwing.</summary>
    Task<bool> TryReserveStockAsync(Guid productId, int quantity, CancellationToken ct = default);

    /// <summary>Compensating action — moves units back from ReservedStock to Stock.</summary>
    Task ReleaseStockAsync(Guid productId, int quantity, CancellationToken ct = default);
}
