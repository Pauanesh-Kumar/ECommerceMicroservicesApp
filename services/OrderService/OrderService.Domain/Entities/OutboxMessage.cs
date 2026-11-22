namespace OrderService.Domain.Entities;
/// <summary>
/// Represents a message to be sent to an external system (e.g., Kafka) as part of the Outbox pattern.
/// </summary>
public sealed class OutboxMessage
{
    public Guid Id { get; set; }
    public string Topic { get; set; } = null!;
    public string Key { get; set; } = null!;
    public string Payload { get; set; } = null!;
    public DateTime OccurredAtUtc { get; set; }
    public DateTime? ProcessedAtUtc { get; set; }
    public bool Sent { get; set; }
    public int Attempts { get; set; }
}