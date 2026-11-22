using MongoDB.Driver;
using ProductService.Domain.Common;
using ProductService.Domain.Entities;
using ProductService.Domain.Repositories;

namespace ProductService.Infrastructure.Persistence;

public sealed class ProductRepository(IMongoDatabase database) : IProductRepository
{
    private readonly IMongoCollection<Product> _collection = database.GetCollection<Product>("products");

    public async Task<Product?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => await _collection.Find(p => p.Id == id).FirstOrDefaultAsync(ct);

    /// <summary>
    /// Keyset pagination: WHERE (CreatedAtUtc, Id) > (cursor.CreatedAtUtc, cursor.Id)
    /// ORDER BY CreatedAtUtc, Id LIMIT (limit+1). The "+1" trick lets us tell whether
    /// there's a next page without a separate COUNT query — if we get limit+1 rows back,
    /// there's more; we trim to `limit` and cursor off the last returned row.
    /// Requires a compound index { CreatedAtUtc: 1, _id: 1 } (see MongoDbInitializer-style
    /// index setup in InfrastructureServiceExtensions) for this to stay O(log n) per page.
    /// </summary>
    public async Task<CursorPageResult<Product>> GetByCursorAsync(
        string? cursor, int limit, string? category, CancellationToken ct = default)
    {
        var filters = new List<FilterDefinition<Product>> { Builders<Product>.Filter.Eq(p => p.IsActive, true) };

        if (!string.IsNullOrWhiteSpace(category))
            filters.Add(Builders<Product>.Filter.Eq(p => p.Category, category));

        var decoded = CursorEncoder.Decode(cursor);
        if (decoded is { } key)
        {
            // Seek predicate: strictly-after the last row of the previous page, in
            // (CreatedAtUtc, Id) order — the standard composite-key keyset condition:
            // (CreatedAtUtc > k.CreatedAtUtc) OR (CreatedAtUtc == k.CreatedAtUtc AND Id > k.Id)
            var seek = Builders<Product>.Filter.Or(
                Builders<Product>.Filter.Gt(p => p.CreatedAtUtc, key.CreatedAtUtc),
                Builders<Product>.Filter.And(
                    Builders<Product>.Filter.Eq(p => p.CreatedAtUtc, key.CreatedAtUtc),
                    Builders<Product>.Filter.Gt(p => p.Id, key.Id)));
            filters.Add(seek);
        }

        var filter = Builders<Product>.Filter.And(filters);

        var rows = await _collection.Find(filter)
            .SortBy(p => p.CreatedAtUtc).ThenBy(p => p.Id)
            .Limit(limit + 1)
            .ToListAsync(ct);

        var hasMore = rows.Count > limit;
        var page = hasMore ? rows.Take(limit).ToList() : rows;
        var nextCursor = page.Count > 0
            ? CursorEncoder.Encode(page[^1].CreatedAtUtc, page[^1].Id)
            : null;

        return new CursorPageResult<Product>(page, hasMore ? nextCursor : null, hasMore);
    }

    public Task AddAsync(Product product, CancellationToken ct = default)
        => _collection.InsertOneAsync(product, cancellationToken: ct);

    // Optimistic concurrency for admin edits (price/description) — guarded by Version.
    public async Task<bool> UpdateAsync(Product product, long expectedVersion, CancellationToken ct = default)
    {
        var filter = Builders<Product>.Filter.And(
            Builders<Product>.Filter.Eq(p => p.Id, product.Id),
            Builders<Product>.Filter.Eq(p => p.Version, expectedVersion));

        var update = Builders<Product>.Update
            .Set(p => p.Name, product.Name)
            .Set(p => p.Description, product.Description)
            .Set(p => p.Price, product.Price)
            .Set(p => p.Category, product.Category)
            .Set(p => p.IsActive, product.IsActive)
            .Set(p => p.UpdatedAtUtc, DateTime.UtcNow)
            .Inc(p => p.Version, 1);

        var result = await _collection.UpdateOneAsync(filter, update, cancellationToken: ct);
        return result.ModifiedCount == 1; // false => version mismatch (someone else edited it first)
    }

    // ── Saga-critical: atomic stock reservation, single round trip, no lost-update race ──
    public async Task<bool> TryReserveStockAsync(Guid productId, int quantity, CancellationToken ct = default)
    {
        var filter = Builders<Product>.Filter.And(
            Builders<Product>.Filter.Eq(p => p.Id, productId),
            Builders<Product>.Filter.Gte(p => p.Stock, quantity)); // guard: only matches if enough stock

        var update = Builders<Product>.Update
            .Inc(p => p.Stock, -quantity)
            .Inc(p => p.ReservedStock, quantity)
            .Set(p => p.UpdatedAtUtc, DateTime.UtcNow);

        var result = await _collection.UpdateOneAsync(filter, update, cancellationToken: ct);
        return result.ModifiedCount == 1;
    }

    public async Task ReleaseStockAsync(Guid productId, int quantity, CancellationToken ct = default)
    {
        var filter = Builders<Product>.Filter.Eq(p => p.Id, productId);
        var update = Builders<Product>.Update
            .Inc(p => p.Stock, quantity)
            .Inc(p => p.ReservedStock, -quantity)
            .Set(p => p.UpdatedAtUtc, DateTime.UtcNow);

        await _collection.UpdateOneAsync(filter, update, cancellationToken: ct);
    }
}
