using AnalyticsService.Domain.Repositories;
using MediatR;

namespace AnalyticsService.Application.Queries.Reporting;

/// <summary>Shared date-window rule: default last 30 days, never wider than a year, always from &lt;= to.</summary>
public readonly record struct ReportWindow(DateTime FromUtc, DateTime ToUtc)
{
    public const int DefaultDays = 30;
    public const int MaxDays = 366;

    public static ReportWindow Normalize(DateTime? from, DateTime? to, DateTime? nowUtc = null)
    {
        var now = nowUtc ?? DateTime.UtcNow;
        var end = to is null ? now : AsUtc(to.Value);
        var start = from is null ? end.AddDays(-DefaultDays) : AsUtc(from.Value);
        if (start > end) (start, end) = (end, start);
        if ((end - start).TotalDays > MaxDays) start = end.AddDays(-MaxDays);
        return new ReportWindow(start, end);
    }

    private static DateTime AsUtc(DateTime value) => value.Kind switch
    {
        DateTimeKind.Utc => value,
        DateTimeKind.Local => value.ToUniversalTime(),
        _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
    };
}

public sealed record GetSalesSummaryQuery(DateTime? From, DateTime? To) : IRequest<SalesSummary>;
public sealed record GetOrdersByStatusQuery(DateTime? From, DateTime? To) : IRequest<IReadOnlyList<OrderStatusBreakdown>>;
public sealed record GetDailyRevenueQuery(DateTime? From, DateTime? To) : IRequest<IReadOnlyList<DailyRevenuePoint>>;
public sealed record GetPaymentsByStatusQuery(DateTime? From, DateTime? To) : IRequest<IReadOnlyList<PaymentStatusBreakdown>>;
public sealed record GetReportingFreshnessQuery : IRequest<ReportingFreshness>;

public sealed class ReportingQueryHandlers(IReportingRepository repo) :
    IRequestHandler<GetSalesSummaryQuery, SalesSummary>,
    IRequestHandler<GetOrdersByStatusQuery, IReadOnlyList<OrderStatusBreakdown>>,
    IRequestHandler<GetDailyRevenueQuery, IReadOnlyList<DailyRevenuePoint>>,
    IRequestHandler<GetPaymentsByStatusQuery, IReadOnlyList<PaymentStatusBreakdown>>,
    IRequestHandler<GetReportingFreshnessQuery, ReportingFreshness>
{
    public Task<SalesSummary> Handle(GetSalesSummaryQuery q, CancellationToken ct)
    {
        var w = ReportWindow.Normalize(q.From, q.To);
        return repo.GetSalesSummaryAsync(w.FromUtc, w.ToUtc, ct);
    }

    public Task<IReadOnlyList<OrderStatusBreakdown>> Handle(GetOrdersByStatusQuery q, CancellationToken ct)
    {
        var w = ReportWindow.Normalize(q.From, q.To);
        return repo.GetOrdersByStatusAsync(w.FromUtc, w.ToUtc, ct);
    }

    public Task<IReadOnlyList<DailyRevenuePoint>> Handle(GetDailyRevenueQuery q, CancellationToken ct)
    {
        var w = ReportWindow.Normalize(q.From, q.To);
        return repo.GetDailyRevenueAsync(w.FromUtc, w.ToUtc, ct);
    }

    public Task<IReadOnlyList<PaymentStatusBreakdown>> Handle(GetPaymentsByStatusQuery q, CancellationToken ct)
    {
        var w = ReportWindow.Normalize(q.From, q.To);
        return repo.GetPaymentsByStatusAsync(w.FromUtc, w.ToUtc, ct);
    }

    public Task<ReportingFreshness> Handle(GetReportingFreshnessQuery q, CancellationToken ct) => repo.GetFreshnessAsync(ct);
}
