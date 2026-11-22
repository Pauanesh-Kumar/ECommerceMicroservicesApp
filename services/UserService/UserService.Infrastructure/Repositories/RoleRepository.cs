using UserService.Domain.Repositories;
using UserService.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using UserService.Domain.Entities;

public sealed class RoleRepository(UserDbContext db) : IRoleRepository
{
    public async Task<Role?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => await db.Roles.AsNoTracking().FirstOrDefaultAsync(r => r.Id == id, ct);

    public async Task<Role?> GetByNameAsync(string name, CancellationToken ct = default)
        => await db.Roles.AsNoTracking().FirstOrDefaultAsync(r => r.Name == name, ct);

    public async Task<IReadOnlyList<Role>> GetAllAsync(bool includeDeleted = false, CancellationToken ct = default)
    {
        var query = db.Roles.AsNoTracking();
        if (!includeDeleted) query = query.Where(r => !r.IsDeleted);
        return await query.OrderBy(r => r.Name).ToListAsync(ct);
    }
}