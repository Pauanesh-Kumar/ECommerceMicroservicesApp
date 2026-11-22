using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using UserService.Domain.Entities;
using UserService.Domain.Repositories;
using UserService.Infrastructure.Persistence;

namespace UserService.Infrastructure.Repositories;

/// <summary>
/// EF Core implementation of IUserRepository.
/// Uses AsNoTracking on all reads — tracking only happens on writes.
/// </summary>
public sealed class UserRepository(UserDbContext db) : IUserRepository
{
    public async Task<User?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => await db.Users
                   .AsNoTracking()
                   .Include(u => u.Role)
                   .FirstOrDefaultAsync(u => u.Id == id, ct);

    public async Task<User?> GetByEmailAsync(string email, CancellationToken ct = default)
        => await db.Users
                   .AsNoTracking()
                   .Include(u => u.Role)
                   .FirstOrDefaultAsync(u => u.Email == email.ToLowerInvariant(), ct);

    public async Task<IReadOnlyList<User>> GetPagedAsync(
        int page, int pageSize, string? statusFilter, CancellationToken ct = default)
    {
        var query = db.Users.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(statusFilter) &&
            Enum.TryParse<UserStatus>(statusFilter, ignoreCase: true, out var status))
            query = query.Include(u => u.Role).Where(u => u.Status == status);

        return await query
            .OrderByDescending(u => u.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);
    }

    public async Task<int> CountAsync(string? statusFilter, CancellationToken ct = default)
    {
        var query = db.Users.AsQueryable();

        if (!string.IsNullOrWhiteSpace(statusFilter) &&
            Enum.TryParse<UserStatus>(statusFilter, ignoreCase: true, out var status))
            query = query.Where(u => u.Status == status);

        return await query.CountAsync(ct);
    }

    public async Task AddAsync(User user, CancellationToken ct = default)
        => await db.Users.AddAsync(user, ct);

    public Task UpdateAsync(User user, CancellationToken ct = default)
    {
        db.Users.Update(user);
        return Task.CompletedTask;
    }

    public async Task<bool> ExistsByEmailAsync(string email, CancellationToken ct = default)
        => await db.Users.AnyAsync(u => u.Email == email.ToLowerInvariant(), ct);
}

