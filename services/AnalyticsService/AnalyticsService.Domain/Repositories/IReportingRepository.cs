namespace AnalyticsService.Domain.Repositories;

public sealed record SalesSummary(
    DateTime FromUtc, DateTime ToUtc,
    int TotalOrders, int PendingOrders, int ConfirmedOrders, int CancelledOrders,
    decimal GrossRevenue, decimal AverageOrderValue,
    int PaymentsSucceeded, int PaymentsFailed);

public sealed record OrderStatusBreakdown(string Status, int Orders, decimal TotalAmount);

public sealed record DailyRevenuePoint(DateOnly Date, int ConfirmedOrders, decimal Revenue);

public sealed record PaymentStatusBreakdown(string Status, int Payments, decimal Amount);

public sealed record ReportingFreshness(
    DateTime? LastProjectedAtUtc, long TotalEventsProjected, int OrderRows, int PaymentRows, DateTime CheckedAtUtc);

/// <summary>Read side of the curated Postgres `reporting` schema (the same schema Power BI connects to).</summary>
public interface IReportingRepository
{
    Task<SalesSummary> GetSalesSummaryAsync(DateTime fromUtc, DateTime toUtc, CancellationToken ct = default);
    Task<IReadOnlyList<OrderStatusBreakdown>> GetOrdersByStatusAsync(DateTime fromUtc, DateTime toUtc, CancellationToken ct = default);
    Task<IReadOnlyList<DailyRevenuePoint>> GetDailyRevenueAsync(DateTime fromUtc, DateTime toUtc, CancellationToken ct = default);
    Task<IReadOnlyList<PaymentStatusBreakdown>> GetPaymentsByStatusAsync(DateTime fromUtc, DateTime toUtc, CancellationToken ct = default);
    Task<ReportingFreshness> GetFreshnessAsync(CancellationToken ct = default);
}
