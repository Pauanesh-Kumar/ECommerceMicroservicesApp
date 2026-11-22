using AnalyticsService.Domain.Entities;
using AnalyticsService.Domain.Repositories;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Npgsql;
using System.Text.Json;

namespace AnalyticsService.Infrastructure.Reporting;

/// <summary>
/// The Mongo→Postgres ETL for Power BI. Runs on a timer (every 15 min by default),
/// reads unprojected AnalyticsEvents, upserts into a curated `reporting` schema star
/// schema in Postgres, marks them projected. THIS is what Power BI's PostgreSQL
/// connector points at — never the raw Mongo bronze layer, never the OLTP Order/Payment
/// databases directly. For heavier volumes, replace this in-process timer with a proper
/// scheduled job (Kafka Connect / Airflow / dbt) — the schema/guarantees stay the same.
/// </summary>
public sealed class ReportingProjectionHostedService(
    IServiceScopeFactory scopeFactory, IConfiguration configuration, ILogger<ReportingProjectionHostedService> logger) : BackgroundService
{
    private static readonly JsonSerializerOptions Json = new() { PropertyNameCaseInsensitive = true };
    private readonly string _connectionString = configuration.GetConnectionString("AnalyticsReportingDb")!;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await EnsureSchemaAsync(stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try { await ProjectBatchAsync(stoppingToken); }
            catch (Exception ex) { logger.LogError(ex, "[AnalyticsService] Reporting projection cycle failed"); }
            await Task.Delay(TimeSpan.FromMinutes(15), stoppingToken);
        }
    }

    private async Task EnsureSchemaAsync(CancellationToken ct)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            CREATE SCHEMA IF NOT EXISTS reporting;

            CREATE TABLE IF NOT EXISTS reporting.fact_orders (
                order_id UUID PRIMARY KEY,
                user_id UUID,
                status TEXT,
                total_amount NUMERIC(18,2),
                created_at TIMESTAMPTZ,
                confirmed_at TIMESTAMPTZ
            );

            CREATE TABLE IF NOT EXISTS reporting.fact_payments (
                payment_id UUID PRIMARY KEY,
                order_id UUID,
                status TEXT,
                amount NUMERIC(18,2),
                processed_at TIMESTAMPTZ
            );
            """;
        await cmd.ExecuteNonQueryAsync(ct);
    }

    private async Task ProjectBatchAsync(CancellationToken ct)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var repo = scope.ServiceProvider.GetRequiredService<IAnalyticsRepository>();

        var batch = await repo.GetUnprojectedBatchAsync(500, ct);
        if (batch.Count == 0) return;

        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);

        foreach (var evt in batch)
            await ProjectOneAsync(conn, evt, ct);

        await repo.MarkProjectedAsync(batch.Select(e => e.Id), ct);
        logger.LogInformation("[AnalyticsService] Projected {Count} events into reporting schema", batch.Count);
    }

    private static async Task ProjectOneAsync(NpgsqlConnection conn, AnalyticsEvent evt, CancellationToken ct)
    {
        using var doc = JsonDocument.Parse(evt.RawPayload);
        var root = doc.RootElement;
        var payloadRoot = root.TryGetProperty("payload", out var inner)
            ? JsonDocument.Parse(inner.GetString() ?? "{}").RootElement
            : root;

        switch (evt.EventType)
        {
            case "OrderConfirmedIntegrationEvent" or "OrderConfirmed":
                await UpsertOrderAsync(conn, payloadRoot, "Confirmed", ct); break;
            case "OrderCancelledIntegrationEvent" or "OrderCancelled":
                await UpsertOrderAsync(conn, payloadRoot, "Cancelled", ct); break;
            case "PaymentProcessed":
                await UpsertPaymentAsync(conn, payloadRoot, "Succeeded", ct); break;
            case "PaymentFailed":
                await UpsertPaymentAsync(conn, payloadRoot, "Failed", ct); break;
        }
    }

    private static async Task UpsertOrderAsync(NpgsqlConnection conn, JsonElement payload, string status, CancellationToken ct)
    {
        if (!payload.TryGetProperty("orderId", out var orderIdEl)) return;
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            INSERT INTO reporting.fact_orders (order_id, user_id, status, total_amount, created_at, confirmed_at)
            VALUES (@orderId, @userId, @status, @amount, now(), CASE WHEN @status = 'Confirmed' THEN now() ELSE NULL END)
            ON CONFLICT (order_id) DO UPDATE SET status = @status,
                confirmed_at = CASE WHEN @status = 'Confirmed' THEN now() ELSE reporting.fact_orders.confirmed_at END;
            """;
        cmd.Parameters.AddWithValue("orderId", orderIdEl.GetGuid());
        cmd.Parameters.AddWithValue("userId", payload.TryGetProperty("userId", out var u) ? u.GetGuid() : Guid.Empty);
        cmd.Parameters.AddWithValue("status", status);
        cmd.Parameters.AddWithValue("amount", payload.TryGetProperty("amount", out var a) ? a.GetDecimal() : 0m);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    private static async Task UpsertPaymentAsync(NpgsqlConnection conn, JsonElement payload, string status, CancellationToken ct)
    {
        if (!payload.TryGetProperty("paymentId", out var paymentIdEl) || paymentIdEl.ValueKind == JsonValueKind.Null) return;
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            INSERT INTO reporting.fact_payments (payment_id, order_id, status, amount, processed_at)
            VALUES (@paymentId, @orderId, @status, @amount, now())
            ON CONFLICT (payment_id) DO UPDATE SET status = @status, processed_at = now();
            """;
        cmd.Parameters.AddWithValue("paymentId", paymentIdEl.GetGuid());
        cmd.Parameters.AddWithValue("orderId", payload.TryGetProperty("orderId", out var o) ? o.GetGuid() : Guid.Empty);
        cmd.Parameters.AddWithValue("status", status);
        cmd.Parameters.AddWithValue("amount", payload.TryGetProperty("amount", out var a) ? a.GetDecimal() : 0m);
        await cmd.ExecuteNonQueryAsync(ct);
    }
}
                                                                                                                                                                              