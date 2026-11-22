namespace PaymentService.Infrastructure.Persistence;

public sealed class OutboxMessage
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public string Topic { get; init; } = default!;
    public string Key { get; init; } = default!;
    public string Payload { get; init; } = default!;
    public DateTime CreatedAtUtc { get; init; } = DateTime.UtcNow;
    public DateTime? ProcessedAtUtc { get; set; }
    public int Attempts { get; set; }
}

/// <summary>Inbox for the payment-requested consumer AND the Razorpay webhook (webhook's
/// `event.id` is inserted here too — Razorpay retries webhooks on non-2xx responses).</summary>
public sealed class InboxMessage
{
    public string EventId { get; init; } = default!;
    public DateTime ProcessedAtUtc { get; init; } = DateTime.UtcNow;
}
