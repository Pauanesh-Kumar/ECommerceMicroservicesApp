namespace OrderService.Domain.Entities;

public sealed class IdempotencyRecord
{
    public string IdempotencyKey { get; set; } = null!;
    public Guid? EventId { get; set; }
    public string? Result { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime ExpiresAtUtc { get; set; }
}
