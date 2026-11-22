using AnalyticsService.Domain.Entities;
using AnalyticsService.Domain.Repositories;
using MongoDB.Bson;
using MongoDB.Driver;

namespace AnalyticsService.Infrastructure.Persistence;

public sealed class AnalyticsRepository(IMongoDatabase database) : IAnalyticsRepository
{
    private readonly IMongoCollection<AnalyticsEvent> _collection = database.GetCollection<AnalyticsEvent>("events");

    public Task InsertAsync(AnalyticsEvent evt, CancellationToken ct = default)
        => _collection.InsertOneAsync(evt, cancellationToken: ct);

    public async Task<IReadOnlyList<AnalyticsEvent>> GetUnprojectedBatchAsync(int batchSize, CancellationToken ct = default)
        => await _collection.Find(e => !e.ProjectedToReporting).Limit(batchSize).ToListAsync(ct);

    public async Task MarkProjectedAsync(IEnumerable<ObjectId> ids, CancellationToken ct = default)
    {
        var filter = Builders<AnalyticsEvent>.Filter.In(e => e.Id, ids);
        var update = Builders<AnalyticsEvent>.Update.Set(e => e.ProjectedToReporting, true);
        await _collection.UpdateManyAsync(filter, update, cancellationToken: ct);
    }

    /// <summary>
    /// Cursor pagination keyed purely on _id (ObjectId). MongoDB's ObjectId embeds a
    /// timestamp + monotonic counter, so `_id > cursor` in ascending sort order IS
    /// chronological order — no composite key needed here, unlike the Guid-keyed
    /// entities elsewhere in this solution.
    /// </summary>
    public async Task<CursorPageResult<AnalyticsEvent>> GetByCursorAsync(
        string? cursor, int limit, string? eventType, CancellationToken ct = default)
    {
        var filters = new List<FilterDefinition<AnalyticsEvent>>();

        if (!string.IsNullOrWhiteSpace(eventType))
            filters.Add(Builders<AnalyticsEvent>.Filter.Eq(e => e.EventType, eventType));

        if (!string.IsNullOrWhiteSpace(cursor) && ObjectId.TryParse(cursor, out var cursorId))
            filters.Add(Builders<AnalyticsEvent>.Filter.Gt(e => e.Id, cursorId));

        var filter = filters.Count > 0 ? Builders<AnalyticsEvent>.Filter.And(filters) : Builders<AnalyticsEvent>.Filter.Empty;

        var rows = await _collection.Find(filter).SortBy(e => e.Id).Limit(limit + 1).ToListAsync(ct);

        var hasMore = rows.Count > limit;
        var page = hasMore ? rows.Take(limit).ToList() : rows;
        var nextCursor = page.Count > 0 ? page[^1].Id.ToString() : null;

        return new CursorPageResult<AnalyticsEvent>(page, hasMore ? nextCursor : null, hasMore);
    }
}
