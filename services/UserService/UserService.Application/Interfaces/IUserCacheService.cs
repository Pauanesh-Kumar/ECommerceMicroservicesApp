using UserService.Domain.Entities;

namespace UserService.Application.Interfaces;
/// <summary>
/// SOLID — Interface Segregation Principle:
/// Each interface below covers exactly ONE capability.
/// Application layer depends on abstractions, never on concrete implementations.
/// Infrastructure layer provides the concrete classes.
/// </summary>


// ── Cache ─────────────────────────────────────────────────────────────────────
public interface IUserCacheService
{
    Task<UserCacheEntry?> GetByIdAsync(Guid userId, CancellationToken ct = default);
    Task<UserCacheEntry?> GetByEmailAsync(string email, CancellationToken ct = default);
    Task SetAsync(UserCacheEntry entry, CancellationToken ct = default);
    Task RemoveAsync(Guid userId, CancellationToken ct = default);
    Task RemoveByEmailAsync(string email, CancellationToken ct = default);
}

public sealed record UserCacheEntry(
    Guid UserId,
    string Email,
    string FirstName,
    string LastName,
    string FullName,
    string? PhoneNumber,
    string Status,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    Guid RoleId,
    Role Role);