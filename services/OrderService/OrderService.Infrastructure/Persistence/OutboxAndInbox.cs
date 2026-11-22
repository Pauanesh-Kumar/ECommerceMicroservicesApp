namespace OrderService.Infrastructure.Persistence;

/// <summary>
/// Transactional Outbox. Written in the SAME EF Core SaveChangesAsync call as the
/// business row it accompanies (via EfOrderUnitOfWork). A background poller
/// (OutboxPublisherHostedService) is the ONLY thing that talks to Kafka for
/// OrderService — guarantees "commit the DB row" and "the event will eventually reach
/// Kafka" happen together.
/// </summary>
public sealed class OutboxMessage
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public string Topic { get; init; } = default!;
    public string Key { get; init; } = default!;
    public string Payload { get; init; } = default!;
    public DateTime CreatedAtUtc { get; init; } = DateTime.UtcNow;
    public DateTime? ProcessedAtUtc { get; set; }
    public int Attempts { get; set; }
    public string? LastError { get; set; }
}

/// <summary>Inbox for OrderService's own Kafka consumers (product-stock, payment-status).
/// Unique index on EventId — redelivery is a no-op.</summary>
public sealed class InboxMessage
{
    public Guid EventId { get; init; }
    public DateTime ProcessedAtUtc { get; init; } = DateTime.UtcNow;
}

/// <summary>Client-facing idempotency (POST /api/orders with Idempotency-Key header).
/// Unique index on IdempotencyKey — a retried checkout returns the ORIGINAL OrderId.</summary>
public sealed class IdempotencyRecord
{
    public string IdempotencyKey { get; init; } = default!;
    public Guid OrderId { get; init; }
    public DateTime CreatedAtUtc { get; init; } = DateTime.UtcNow;
}
