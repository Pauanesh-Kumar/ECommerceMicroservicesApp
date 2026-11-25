using AnalyticsService.Application.Queries.GetAnalyticsEvents;
using AnalyticsService.Application.Queries.Reporting;
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

        // Curated reporting API over the dedicated reporting database. `from`/`to` are optional ISO-8601
        // timestamps (default: last 30 days, max window 366 days).
        var reports = analytics.MapGroup("/reports").WithTags("Analytics Reports");

        reports.MapGet("/summary", async (DateTime? from, DateTime? to, ISender mediator, CancellationToken ct) =>
            Results.Ok(await mediator.Send(new GetSalesSummaryQuery(from, to), ct)))
            .WithSummary("Orders, revenue (confirmed orders), average order value and payment outcomes");

        reports.MapGet("/orders-by-status", async (DateTime? from, DateTime? to, ISender mediator, CancellationToken ct) =>
            Results.Ok(await mediator.Send(new GetOrdersByStatusQuery(from, to), ct)))
            .WithSummary("Order count and amount per status");

        reports.MapGet("/daily-revenue", async (DateTime? from, DateTime? to, ISender mediator, CancellationToken ct) =>
            Results.Ok(await mediator.Send(new GetDailyRevenueQuery(from, to), ct)))
            .WithSummary("Confirmed-order revenue per UTC day");

        reports.MapGet("/payments-by-status", async (DateTime? from, DateTime? to, ISender mediator, CancellationToken ct) =>
            Results.Ok(await mediator.Send(new GetPaymentsByStatusQuery(from, to), ct)))
            .WithSummary("Payment count and amount per status");

        reports.MapGet("/freshness", async (ISender mediator, CancellationToken ct) =>
            Results.Ok(await mediator.Send(new GetReportingFreshnessQuery(), ct)))
            .WithSummary("When the reporting model was last refreshed and how much it holds");

        return app;
    }
}
