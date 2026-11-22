namespace PaymentService.Domain.Entities;
/// <summary>
/// Represents a message received from an external system (e.g., Kafka) as part of the Inbox pattern.
/// </summary>
public sealed class InboxMessage
{
    public Guid EventId { get; set; }
    public string EventType { get; set; } = null!;
    public string Payload { get; set; } = null!;
    public DateTime ReceivedAtUtc { get; set; }
    public bool Processed { get; set; }
}