using AnalyticsService.Application.Queries.GetAnalyticsEvents;
using MediatR;

namespace AnalyticsService.API.Endpoints;

public static class AnalyticsEndpoints
{
    public static IEndpointRouteBuilder MapAnalyticsEndpoints(this IEndpointRouteBuilder app)
    {
        var analytics = app.MapGroup("/api/analytics").WithTags("Analytics").WithOpenApi().RequireAuthorization("Admin");

        // Ad-hoc / Power BI "Web" connector source. For production dashboards prefer the
        // curated Postgres `reporting` schema (see ReportingProjectionHostedService) —
        // this endpoint is for quick exploration and lightweight embedded widgets only.
        // Cursor pagination: pass `cursor` from the previous response's nextCursor.
        analytics.MapGet("/events", async (string? cursor, int? limit, string? eventType, ISender mediator, CancellationToken ct) =>
        {
            var page = await mediator.Send(new GetAnalyticsEventsQuery(cursor, limit ?? 200, eventType), ct);
            return Results.Ok(new { items = page.Items, nextCursor = page.NextCursor, hasMore = page.HasMore });
        })
        .WithSummary("Cursor-paginated raw analytics events (bronze layer)");

        return app;
    }
}
