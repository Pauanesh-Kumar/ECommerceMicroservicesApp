using AnalyticsService.Application.Common;
using AnalyticsService.Domain.Entities;
using AnalyticsService.Domain.Repositories;
using MediatR;

namespace AnalyticsService.Application.Queries.GetAnalyticsEvents;

public sealed record GetAnalyticsEventsQuery(string? Cursor, int Limit, string? EventType) : IRequest<CursorPage<AnalyticsEvent>>;

public sealed class GetAnalyticsEventsQueryHandler(IAnalyticsRepository repo)
    : IRequestHandler<GetAnalyticsEventsQuery, CursorPage<AnalyticsEvent>>
{
    public async Task<CursorPage<AnalyticsEvent>> Handle(GetAnalyticsEventsQuery query, CancellationToken ct)
    {
        var limit = query.Limit is <= 0 or > 500 ? 200 : query.Limit;
        var result = await repo.GetByCursorAsync(query.Cursor, limit, query.EventType, ct);
        return new CursorPage<AnalyticsEvent>(result.Items, result.NextCursor, result.HasMore);
    }
}
