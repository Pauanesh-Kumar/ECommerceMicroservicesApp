using AnalyticsService.Domain.Repositories;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace AnalyticsService.Infrastructure.Reporting;

/// <summary>
/// The Mongo→Postgres projection for Power BI and the /api/analytics/reports endpoints. Every
/// `Reporting:ProjectionIntervalSeconds` (default 60) it drains unprojected bronze events in batches,
/// upserts them into the curated `reporting` star schema (one transaction per batch, all upserts idempotent
/// and order-independent), then marks them projected. Consumers read the dedicated reporting database,
/// never the raw Mongo layer or the OLTP Order/Payment databases.
/// </summary>
public sealed class ReportingProjectionHostedService(
    IServiceScopeFactory scopeFactory, IConfiguration configuration, ILogger<ReportingProjectionHostedService> logger) : BackgroundService
{
    private const int BatchSize = 500;
    private readonly string _connectionString = configuration.GetConnectionString("AnalyticsReportingDb")!;

    private TimeSpan Interval => TimeSpan.FromSeconds(Math.Clamp(configuration.GetValue("Reporting:ProjectionIntervalSeconds", 60), 5, 3600));

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // The reporting DB container may still be starting; keep retrying instead of crashing the host.
        while (!stoppingToken.IsCancellationRequested)
        {
            try { await ReportingSchema.EnsureAsync(_connectionString, stoppingToken); break; }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                logger.LogWarning(ex, "[AnalyticsService] Reporting schema not ready; retrying in 10s");
                await DelayAsync(TimeSpan.FromSeconds(10), stoppingToken);
            }
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                int projected;
                do { projected = await ProjectBatchAsync(stoppingToken); } while (projected == BatchSize);
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                logger.LogError(ex, "[AnalyticsService] Reporting projection cycle failed");
            }
            await DelayAsync(Interval, stoppingToken);
        }
    }

    private static async Task DelayAsync(TimeSpan delay, CancellationToken ct)
    {
        try { await Task.Delay(delay, ct); }
        catch (OperationCanceledException) { /* shutting down */ }
    }

    private async Task<int> ProjectBatchAsync(CancellationToken ct)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var repo = scope.ServiceProvider.GetRequiredService<IAnalyticsRepository>();

        var batch = await repo.GetUnprojectedBatchAsync(BatchSize, ct);
        if (batch.Count == 0) return 0;

        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);
        await using var tx = await conn.BeginTransactionAsync(ct);

        var applied = 0;
        foreach (var evt in batch.OrderBy(e => e.Id))
        {
            var change = ReportingEventMapper.Map(evt.EventType, evt.RawPayload, evt.IngestedAtUtc);
            if (change is null) continue;
            await ApplyAsync(conn, tx, change, ct);
            applied++;
        }

        await using (var state = conn.CreateCommand())
        {
            state.Transaction = tx;
            state.CommandText = """
                INSERT INTO reporting.projection_state (id, last_projected_at, events_projected)
                VALUES (1, now(), @n)
                ON CONFLICT (id) DO UPDATE SET last_projected_at = now(),
                    events_projected = reporting.projection_state.events_projected + @n;
                """;
            state.Parameters.AddWithValue("n", (long)applied);
            await state.ExecuteNonQueryAsync(ct);
        }
        await tx.CommitAsync(ct);

        await repo.MarkProjectedAsync(batch.Select(e => e.Id), ct);
        logger.LogInformation("[AnalyticsService] Projected {Applied}/{Count} events into reporting schema", applied, batch.Count);
        return batch.Count;
    }

    private static Task ApplyAsync(NpgsqlConnection conn, NpgsqlTransaction tx, ReportingChange change, CancellationToken ct) => change switch
    {
        OrderCreatedChange c => ExecAsync(conn, tx, """
            INSERT INTO reporting.fact_orders (order_id, user_id, status, total_amount, created_at)
            VALUES (@orderId, @userId, 'Pending', 0, @at)
            ON CONFLICT (order_id) DO UPDATE SET
                user_id = COALESCE(reporting.fact_orders.user_id, EXCLUDED.user_id),
                created_at = LEAST(COALESCE(reporting.fact_orders.created_at, EXCLUDED.created_at), EXCLUDED.created_at);
            """, ct, ("orderId", c.OrderId), ("userId", (object?)c.UserId ?? DBNull.Value), ("at", c.AtUtc)),

        OrderConfirmedChange f => ExecAsync(conn, tx, """
            INSERT INTO reporting.fact_orders (order_id, user_id, status, total_amount, created_at, confirmed_at)
            VALUES (@orderId, @userId, 'Confirmed', @amount, @at, @at)
            ON CONFLICT (order_id) DO UPDATE SET
                status = 'Confirmed',
                user_id = COALESCE(reporting.fact_orders.user_id, EXCLUDED.user_id),
                total_amount = CASE WHEN EXCLUDED.total_amount > 0 THEN EXCLUDED.total_amount ELSE reporting.fact_orders.total_amount END,
                confirmed_at = EXCLUDED.confirmed_at,
                created_at = COALESCE(reporting.fact_orders.created_at, EXCLUDED.created_at);
            UPDATE reporting.fact_payments SET amount = @amount WHERE order_id = @orderId AND amount = 0 AND @amount > 0;
            """, ct, ("orderId", f.OrderId), ("userId", (object?)f.UserId ?? DBNull.Value), ("amount", f.Amount), ("at", f.AtUtc)),

        OrderCancelledChange x => ExecAsync(conn, tx, """
            INSERT INTO reporting.fact_orders (order_id, user_id, status, total_amount, created_at, cancelled_at)
            VALUES (@orderId, @userId, 'Cancelled', 0, @at, @at)
            ON CONFLICT (order_id) DO UPDATE SET
                status = CASE WHEN reporting.fact_orders.status = 'Confirmed' THEN reporting.fact_orders.status ELSE 'Cancelled' END,
                user_id = COALESCE(reporting.fact_orders.user_id, EXCLUDED.user_id),
                cancelled_at = EXCLUDED.cancelled_at,
                created_at = COALESCE(reporting.fact_orders.created_at, EXCLUDED.created_at);
            """, ct, ("orderId", x.OrderId), ("userId", (object?)x.UserId ?? DBNull.Value), ("at", x.AtUtc)),

        PaymentChange p => ExecAsync(conn, tx, """
            INSERT INTO reporting.fact_payments (payment_id, order_id, status, amount, processed_at)
            VALUES (@paymentId, @orderId, @status,
                    COALESCE((SELECT total_amount FROM reporting.fact_orders WHERE order_id = @orderId), 0), @at)
            ON CONFLICT (payment_id) DO UPDATE SET status = EXCLUDED.status, processed_at = EXCLUDED.processed_at;
            """, ct, ("paymentId", p.PaymentId), ("orderId", p.OrderId), ("status", p.Status), ("at", p.AtUtc)),

        _ => Task.CompletedTask
    };

    private static async Task ExecAsync(NpgsqlConnection conn, NpgsqlTransaction tx, string sql, CancellationToken ct, params (string Name, object Value)[] args)
    {
        await using var cmd = conn.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = sql;
        foreach (var (name, value) in args) cmd.Parameters.AddWithValue(name, value);
        await cmd.ExecuteNonQueryAsync(ct);
    }
}
