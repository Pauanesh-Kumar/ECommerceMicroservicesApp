using AnalyticsService.Domain.Repositories;
using Microsoft.Extensions.Configuration;
using Npgsql;

namespace AnalyticsService.Infrastructure.Reporting;

/// <summary>Read-only queries over the `reporting` schema. Order cohort = order's created date (falls back to
/// confirmed/cancelled date); revenue = Confirmed orders; daily revenue is bucketed by confirmation date (UTC).</summary>
public sealed class ReportingRepository(IConfiguration configuration) : IReportingRepository
{
    private const string OrderDate = "COALESCE(created_at, confirmed_at, cancelled_at)";
    private readonly string _connectionString = configuration.GetConnectionString("AnalyticsReportingDb")
        ?? throw new InvalidOperationException("ConnectionStrings:AnalyticsReportingDb is not configured.");

    public async Task<SalesSummary> GetSalesSummaryAsync(DateTime fromUtc, DateTime toUtc, CancellationToken ct = default)
    {
        await using var conn = await OpenAsync(ct);
        int total = 0, pending = 0, confirmed = 0, cancelled = 0;
        decimal revenue = 0;

        await using (var cmd = Command(conn, $"""
            SELECT count(*),
                   count(*) FILTER (WHERE status = 'Pending'),
                   count(*) FILTER (WHERE status = 'Confirmed'),
                   count(*) FILTER (WHERE status = 'Cancelled'),
                   COALESCE(sum(total_amount) FILTER (WHERE status = 'Confirmed'), 0)
            FROM reporting.fact_orders
            WHERE {OrderDate} >= @from AND {OrderDate} <= @to
            """, fromUtc, toUtc))
        await using (var r = await cmd.ExecuteReaderAsync(ct))
        {
            if (await r.ReadAsync(ct))
            {
                total = (int)r.GetInt64(0); pending = (int)r.GetInt64(1); confirmed = (int)r.GetInt64(2); cancelled = (int)r.GetInt64(3);
                revenue = r.GetDecimal(4);
            }
        }

        int succeeded = 0, failed = 0;
        await using (var cmd = Command(conn, """
            SELECT count(*) FILTER (WHERE status = 'Succeeded'), count(*) FILTER (WHERE status = 'Failed')
            FROM reporting.fact_payments WHERE processed_at >= @from AND processed_at <= @to
            """, fromUtc, toUtc))
        await using (var r = await cmd.ExecuteReaderAsync(ct))
        {
            if (await r.ReadAsync(ct)) { succeeded = (int)r.GetInt64(0); failed = (int)r.GetInt64(1); }
        }

        var aov = confirmed == 0 ? 0m : Math.Round(revenue / confirmed, 2);
        return new SalesSummary(fromUtc, toUtc, total, pending, confirmed, cancelled, revenue, aov, succeeded, failed);
    }

    public async Task<IReadOnlyList<OrderStatusBreakdown>> GetOrdersByStatusAsync(DateTime fromUtc, DateTime toUtc, CancellationToken ct = default)
    {
        await using var conn = await OpenAsync(ct);
        await using var cmd = Command(conn, $"""
            SELECT COALESCE(status, 'Unknown'), count(*), COALESCE(sum(total_amount), 0)
            FROM reporting.fact_orders
            WHERE {OrderDate} >= @from AND {OrderDate} <= @to
            GROUP BY 1 ORDER BY 1
            """, fromUtc, toUtc);
        var list = new List<OrderStatusBreakdown>();
        await using var r = await cmd.ExecuteReaderAsync(ct);
        while (await r.ReadAsync(ct)) list.Add(new OrderStatusBreakdown(r.GetString(0), (int)r.GetInt64(1), r.GetDecimal(2)));
        return list;
    }

    public async Task<IReadOnlyList<DailyRevenuePoint>> GetDailyRevenueAsync(DateTime fromUtc, DateTime toUtc, CancellationToken ct = default)
    {
        await using var conn = await OpenAsync(ct);
        await using var cmd = Command(conn, """
            SELECT (confirmed_at AT TIME ZONE 'UTC')::date AS day, count(*), COALESCE(sum(total_amount), 0)
            FROM reporting.fact_orders
            WHERE status = 'Confirmed' AND confirmed_at >= @from AND confirmed_at <= @to
            GROUP BY 1 ORDER BY 1
            """, fromUtc, toUtc);
        var list = new List<DailyRevenuePoint>();
        await using var r = await cmd.ExecuteReaderAsync(ct);
        while (await r.ReadAsync(ct)) list.Add(new DailyRevenuePoint(r.GetFieldValue<DateOnly>(0), (int)r.GetInt64(1), r.GetDecimal(2)));
        return list;
    }

    public async Task<IReadOnlyList<PaymentStatusBreakdown>> GetPaymentsByStatusAsync(DateTime fromUtc, DateTime toUtc, CancellationToken ct = default)
    {
        await using var conn = await OpenAsync(ct);
        await using var cmd = Command(conn, """
            SELECT COALESCE(status, 'Unknown'), count(*), COALESCE(sum(amount), 0)
            FROM reporting.fact_payments
            WHERE processed_at >= @from AND processed_at <= @to
            GROUP BY 1 ORDER BY 1
            """, fromUtc, toUtc);
        var list = new List<PaymentStatusBreakdown>();
        await using var r = await cmd.ExecuteReaderAsync(ct);
        while (await r.ReadAsync(ct)) list.Add(new PaymentStatusBreakdown(r.GetString(0), (int)r.GetInt64(1), r.GetDecimal(2)));
        return list;
    }

    public async Task<ReportingFreshness> GetFreshnessAsync(CancellationToken ct = default)
    {
        await using var conn = await OpenAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT (SELECT last_projected_at FROM reporting.projection_state WHERE id = 1),
                   COALESCE((SELECT events_projected FROM reporting.projection_state WHERE id = 1), 0),
                   (SELECT count(*) FROM reporting.fact_orders),
                   (SELECT count(*) FROM reporting.fact_payments)
            """;
        await using var r = await cmd.ExecuteReaderAsync(ct);
        await r.ReadAsync(ct);
        DateTime? last = r.IsDBNull(0) ? null : DateTime.SpecifyKind(r.GetDateTime(0), DateTimeKind.Utc);
        return new ReportingFreshness(last, r.GetInt64(1), (int)r.GetInt64(2), (int)r.GetInt64(3), DateTime.UtcNow);
    }

    private async Task<NpgsqlConnection> OpenAsync(CancellationToken ct)
    {
        await ReportingSchema.EnsureAsync(_connectionString, ct);
        var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);
        return conn;
    }

    private static NpgsqlCommand Command(NpgsqlConnection conn, string sql, DateTime fromUtc, DateTime toUtc)
    {
        var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        cmd.Parameters.AddWithValue("from", fromUtc);
        cmd.Parameters.AddWithValue("to", toUtc);
        return cmd;
    }
}
