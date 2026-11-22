using System.Text.Json;
using CartService.Domain.Entities;
using CartService.Domain.Repositories;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace CartService.Infrastructure.Persistence;

/// <summary>
/// One Redis STRING per cart (JSON-serialized), key "cart:{userId}", TTL renewed on
/// every write. No pagination needed here — a cart is a small, single-owner document,
/// not a growing collection you'd ever page through.
/// </summary>
public sealed class RedisCartRepository(IConnectionMultiplexer redis, IOptions<CartOptions> options) : IRedisCartRepository
{
    private readonly IDatabase _db = redis.GetDatabase();
    private readonly CartOptions _opts = options.Value;
    private static readonly JsonSerializerOptions Json = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    private static string Key(Guid userId) => $"cart:{userId}";

    public async Task<Cart> GetAsync(Guid userId, CancellationToken ct = default)
    {
        var value = await _db.StringGetAsync(Key(userId));
        if (!value.HasValue) return new Cart(userId, []);
        return JsonSerializer.Deserialize<Cart>(value.ToString()!, Json) ?? new Cart(userId, []);
    }

    public async Task SaveAsync(Cart cart, CancellationToken ct = default)
    {
        var json = JsonSerializer.Serialize(cart, Json);
        await _db.StringSetAsync(Key(cart.UserId), json, TimeSpan.FromDays(_opts.TtlDays));
    }

    public Task ClearAsync(Guid userId, CancellationToken ct = default) => _db.KeyDeleteAsync(Key(userId));
}

public sealed class CartOptions
{
    public const string Section = "Cart";
    public int TtlDays { get; init; } = 7;
}
