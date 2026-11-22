using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace ECommerce.Shared.Infrastructure.Redis;

/// <summary>
/// Redis-backed idempotency key store for preventing duplicate request processing.
/// </summary>

public class RedisIdempotencyStore : IIdempotencyStore
{
    private readonly IDatabase _db;
    private readonly ILogger<RedisIdempotencyStore> _logger;
    private const string KeyPrefix = "idempotency:";
    private static readonly TimeSpan DefaultTtl = TimeSpan.FromHours(24);

    public RedisIdempotencyStore(IConnectionMultiplexer redis, ILogger<RedisIdempotencyStore> logger)
    {
        _db = redis.GetDatabase();
        _logger = logger;
    }

    private static string BuildKey(string idempotencyKey, string userId)
        => $"{KeyPrefix}{userId}:{idempotencyKey}";

    public async Task<string?> GetAsync(string idempotencyKey, string userId)
    {
        try
        {
            var key = BuildKey(idempotencyKey, userId);
            var value = await _db.StringGetAsync(key);
            if (value.HasValue)
            {
                _logger.LogInformation("Idempotency cache hit: '{Key}'", key);
                return value.ToString();
            }
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving idempotency key");
            return null;
        }
    }

    public async Task SetAsync(string idempotencyKey, string userId, string response, TimeSpan? ttl = null)
    {
        try
        {
            var key = BuildKey(idempotencyKey, userId);
            var expiry = ttl ?? DefaultTtl;
            await _db.StringSetAsync(key, response, expiry);
            _logger.LogInformation("Idempotency key stored: '{Key}' TTL={Ttl}h", key, expiry.TotalHours);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error storing idempotency key");
        }
    }

    public async Task<bool> ExistsAsync(string idempotencyKey, string userId)
    {
        try
        {
            var key = BuildKey(idempotencyKey, userId);
            return await _db.KeyExistsAsync(key);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking idempotency key");
            return false;
        }
    }
}