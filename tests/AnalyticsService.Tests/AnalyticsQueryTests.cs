using AnalyticsService.Application.Queries.GetAnalyticsEvents;
using AnalyticsService.Domain.Entities;
using AnalyticsService.Domain.Repositories;
using MongoDB.Bson;

namespace AnalyticsService.Tests;

public class AnalyticsQueryTests
{
    [Theory]
    [InlineData(0, 200)]
    [InlineData(501, 200)]
    [InlineData(25, 25)]
    [InlineData(500, 500)]
    public async Task QueryBoundsLimitAndForwardsCursorAndEventType(int requested, int expected)
    {
        var repo = new FakeRepository();
        var result = await new GetAnalyticsEventsQueryHandler(repo).Handle(new("cursor", requested, "OrderCreated"), default);
        Assert.Equal(expected, repo.Limit);
        Assert.Equal("cursor", repo.Cursor);
        Assert.Equal("OrderCreated", repo.EventType);
        Assert.Equal("next", result.NextCursor);
        Assert.True(result.HasMore);
        Assert.Single(result.Items);
    }

    private sealed class FakeRepository : IAnalyticsRepository
    {
        public int Limit { get; private set; }
        public string? Cursor { get; private set; }
        public string? EventType { get; private set; }
        public Task<CursorPageResult<AnalyticsEvent>> GetByCursorAsync(string? cursor, int limit, string? eventType, CancellationToken ct = default)
        {
            Cursor = cursor; Limit = limit; EventType = eventType;
            return Task.FromResult(new CursorPageResult<AnalyticsEvent>([new AnalyticsEvent()], "next", true));
        }
        public Task InsertAsync(AnalyticsEvent evt, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<AnalyticsEvent>> GetUnprojectedBatchAsync(int batchSize, CancellationToken ct = default) => throw new NotSupportedException();
        public Task MarkProjectedAsync(IEnumerable<ObjectId> ids, CancellationToken ct = default) => throw new NotSupportedException();
    }
}
