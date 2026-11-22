namespace ECommerce.Shared.Domain;

/// <summary>
/// Transactional Outbox Pattern: Ensures domain events are published reliably to Kafka
/// Write event + OutboxMessage in same transaction, marks PublishedAt after Kafka publish
/// CAP Theorem: Consistency + Partition tolerance (prioritizes guaranteed delivery)
/// </summary>
public class OutboxMessage
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string AggregateId { get; set; } = string.Empty;

    public string AggregateType { get; set; } = string.Empty;

    /// <summary>Event type (e.g., "OrderCreated", "PaymentProcessed")</summary>
    public string EventType { get; set; } = string.Empty;

    /// <summary>Serialized event payload (JSON)</summary>
    public string Payload { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Null = not published yet; set by OutboxPublisherBackgroundService</summary>
    public DateTime? PublishedAt { get; set; }

    /// <summary>Retry count for failed publishes (max 3 before DLQ)</summary>
    public int RetryCount { get; set; } = 0;

    public string? CorrelationId { get; set; }

    /// <summary>EF Core optimistic concurrency token</summary>
    public byte[]? RowVersion { get; set; }
}