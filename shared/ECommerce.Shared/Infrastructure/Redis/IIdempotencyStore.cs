namespace ECommerce.Shared.Infrastructure.Redis;

/// <summary>
/// Redis-backed idempotency key store for preventing duplicate request processing.
/// </summary>
public interface IIdempotencyStore
{
    Task<string?> GetAsync(string idempotencyKey, string userId);
    Task SetAsync(string idempotencyKey, string userId, string response, TimeSpan? ttl = null);
    Task<bool> ExistsAsync(string idempotencyKey, string userId);
}
