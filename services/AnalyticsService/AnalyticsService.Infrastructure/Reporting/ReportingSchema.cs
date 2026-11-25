using Npgsql;

namespace AnalyticsService.Infrastructure.Reporting;

/// <summary>Idempotent DDL for the curated `reporting` schema, shared by the projection writer and the report reader.</summary>
public static class ReportingSchema
{
    private const string Ddl = """
        CREATE SCHEMA IF NOT EXISTS reporting;

        CREATE TABLE IF NOT EXISTS reporting.fact_orders (
            order_id UUID PRIMARY KEY,
            user_id UUID,
            status TEXT,
            total_amount NUMERIC(18,2),
            created_at TIMESTAMPTZ,
            confirmed_at TIMESTAMPTZ
        );
        ALTER TABLE reporting.fact_orders ADD COLUMN IF NOT EXISTS cancelled_at TIMESTAMPTZ;

        CREATE TABLE IF NOT EXISTS reporting.fact_payments (
            payment_id UUID PRIMARY KEY,
            order_id UUID,
            status TEXT,
            amount NUMERIC(18,2),
            processed_at TIMESTAMPTZ
        );

        CREATE TABLE IF NOT EXISTS reporting.projection_state (
            id INT PRIMARY KEY,
            last_projected_at TIMESTAMPTZ,
            events_projected BIGINT NOT NULL DEFAULT 0
        );

        CREATE INDEX IF NOT EXISTS ix_fact_orders_created_at ON reporting.fact_orders (created_at);
        CREATE INDEX IF NOT EXISTS ix_fact_orders_confirmed_at ON reporting.fact_orders (confirmed_at);
        CREATE INDEX IF NOT EXISTS ix_fact_payments_processed_at ON reporting.fact_payments (processed_at);
        CREATE INDEX IF NOT EXISTS ix_fact_payments_order_id ON reporting.fact_payments (order_id);
        """;

    private static readonly SemaphoreSlim Gate = new(1, 1);
    private static volatile bool _ensured;

    public static async Task EnsureAsync(string connectionString, CancellationToken ct)
    {
        if (_ensured) return;
        await Gate.WaitAsync(ct);
        try
        {
            if (_ensured) return;
            await using var conn = new NpgsqlConnection(connectionString);
            await conn.OpenAsync(ct);
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = Ddl;
            await cmd.ExecuteNonQueryAsync(ct);
            _ensured = true;
        }
        finally { Gate.Release(); }
    }
}
