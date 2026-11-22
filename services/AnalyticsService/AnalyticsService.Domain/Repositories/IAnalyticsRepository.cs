using AnalyticsService.Domain.Entities;

namespace AnalyticsService.Domain.Repositories;

public sealed record CursorPageResult<T>(IReadOnlyList<T> Items, string? NextCursor, bool HasMore);

public interface IAnalyticsRepository
{
    Task InsertAsync(AnalyticsEvent evt, CancellationToken ct = default);
    Task<IReadOnlyList<AnalyticsEvent>> GetUnprojectedBatchAsync(int batchSize, CancellationToken ct = default);
    Task MarkProjectedAsync(IEnumerable<MongoDB.Bson.ObjectId> ids, CancellationToken ct = default);

    /// <summary>Cursor pagination using MongoDB's ObjectId directly as the cursor — an
    /// ObjectId already embeds a timestamp + monotonic counter, so it is inherently
    /// sortable in insertion order with no need for a separate composite key (unlike
    /// the Guid-keyed entities in Product/Order/Payment, which need the explicit
    /// (CreatedAtUtc, Id) composite because Guid.NewGuid() is NOT time-ordered).</summary>
    Task<CursorPageResult<AnalyticsEvent>> GetByCursorAsync(string? cursor, int limit, string? eventType, CancellationToken ct = default);
}
