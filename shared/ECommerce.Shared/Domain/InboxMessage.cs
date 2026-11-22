namespace ECommerce.Shared.Domain;

/// <summary>
/// Inbox entity for idempotent Kafka event consumption.
/// Prevents duplicate processing of same event across retries.
/// </summary>
public class InboxMessage
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>
    /// Unique external event ID (e.g., Kafka message offset + partition).</summary>
    public string ExternalEventId { get; set; } = string.Empty;

    /// <summary>
    /// Kafka topic name source.</summary>
    public string TopicName { get; set; } = string.Empty;

    /// <summary>
    /// Event payload (JSON).</summary>
    public string Payload { get; set; } = string.Empty;

    /// <summary>
    /// Event type (e.g., "OrderCreated", "PaymentProcessed").</summary>
    public string EventType { get; set; } = string.Empty;

    /// <summary>
    /// Correlation ID for distributed tracing.</summary>
    public string CorrelationId { get; set; } = string.Empty;

    /// <summary>
    /// When received from Kafka.</summary>
    public DateTime ReceivedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// When successfully processed (null = pending).</summary>
    public DateTime? ProcessedAt { get; set; }

    /// <summary>
    /// Processing status (Pending, Processed, Failed).</summary>
    public string Status { get; set; } = "Pending";

    /// <summary>
    /// Last error message (if processing failed).</summary>
    public string? Error { get; set; }

    /// <summary>
    /// Concurrency token for EF Core optimistic locking.</summary>
    public byte[]? RowVersion { get; set; }
}
