using CartService.Domain.Entities;

namespace CartService.Domain.Repositories;

public interface IRedisCartRepository
{
    Task<Cart> GetAsync(Guid userId, CancellationToken ct = default);
    Task SaveAsync(Cart cart, CancellationToken ct = default);
    Task ClearAsync(Guid userId, CancellationToken ct = default);
}
