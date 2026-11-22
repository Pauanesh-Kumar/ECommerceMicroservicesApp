namespace ECommerce.Shared.Domain;

/// <summary>
/// Idempotency Pattern: Request deduplication via Redis + DB backup
/// Key format: "{idempotencyKey}:{userId}" with 24h TTL
/// </summary>
public class IdempotencyRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string IdempotencyKey { get; set; } = string.Empty;

    public string UserId { get; set; } = string.Empty;

    public string RequestMethod { get; set; } = string.Empty;

    public string RequestPath { get; set; } = string.Empty;

    public string RequestPayload { get; set; } = string.Empty;

    public string ResponsePayload { get; set; } = string.Empty;

    public int ResponseStatusCode { get; set; } = 200;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>TTL: 24 hours (soft delete via background job)</summary>
    public DateTime ExpiresAt { get; set; } = DateTime.UtcNow.AddHours(24);

    public byte[]? RowVersion { get; set; }
}