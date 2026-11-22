namespace PaymentService.Domain.Entities;

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