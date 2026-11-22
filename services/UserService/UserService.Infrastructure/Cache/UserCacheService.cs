using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StackExchange.Redis;
using System.Text.Json;
using UserService.Application.Interfaces;

namespace UserService.Infrastructure.Cache;

public sealed class UserCacheService : IUserCacheService
{
    private readonly IDatabase _db;
    private readonly CacheOptions _opts;
    private readonly ILogger<UserCacheService> _logger;

    private static readonly JsonSerializerOptions _json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    public UserCacheService(
        IConnectionMultiplexer redis,
        IOptions<CacheOptions> options,
        ILogger<UserCacheService> logger)
    {
        _db = redis.GetDatabase();
        _opts = options.Value;
        _logger = logger;
    }

    private static string UserIdKey(Guid id) => $"user:id:{id}";
    private static string UserEmailKey(string email) => $"user:email:{email.ToLowerInvariant()}";

    public Task<UserCacheEntry?> GetByIdAsync(Guid userId, CancellationToken ct = default)
        => GetAsync(UserIdKey(userId));

    public Task<UserCacheEntry?> GetByEmailAsync(string email, CancellationToken ct = default)
        => GetAsync(UserEmailKey(email));

    public async Task SetAsync(UserCacheEntry entry, CancellationToken ct = default)
    {
        var json = JsonSerializer.Serialize(entry, _json);
        var ttl = TimeSpan.FromMinutes(_opts.UserTtlMinutes);

        try
        {
            var batch = _db.CreateBatch();
            var t1 = batch.StringSetAsync(UserIdKey(entry.UserId), json, ttl);
            var t2 = batch.StringSetAsync(UserEmailKey(entry.Email), json, ttl);
            batch.Execute();
            await Task.WhenAll(t1, t2);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Redis SET failed for user {UserId} — continuing without cache", entry.UserId);
        }
    }

    public async Task RemoveAsync(Guid userId, CancellationToken ct = default)
    {
        try { await _db.KeyDeleteAsync(UserIdKey(userId)); }
        catch (Exception ex) { _logger.LogWarning(ex, "Redis DELETE failed for userId={UserId}", userId); }
    }

    public async Task RemoveByEmailAsync(string email, CancellationToken ct = default)
    {
        try { await _db.KeyDeleteAsync(UserEmailKey(email)); }
        catch (Exception ex) { _logger.LogWarning(ex, "Redis DELETE failed for email={Email}", email); }
    }

    private async Task<UserCacheEntry?> GetAsync(string key)
    {
        try
        {
            var value = await _db.StringGetAsync(key);
            if (!value.HasValue) return null;
            return JsonSerializer.Deserialize<UserCacheEntry>(value.ToString()!, _json);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Redis GET failed for key={Key} — falling back to DB", key);
            return null;
        }
    }
}

public sealed class CacheOptions
{
    public const string Section = "Cache";
    public int UserTtlMinutes { get; init; } = 15;
}