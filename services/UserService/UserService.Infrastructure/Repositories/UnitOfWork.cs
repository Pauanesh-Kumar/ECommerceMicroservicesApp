using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using UserService.Domain.Entities;
using UserService.Domain.Repositories;
using UserService.Infrastructure.Persistence;

namespace UserService.Infrastructure.Repositories;

/// <summary>
/// Unit of Work wraps EF Core SaveChanges + transaction management.
/// </summary>
public sealed class UnitOfWork(UserDbContext db) : IUnitOfWork
{
    private IDbContextTransaction? _tx;

    public Task<int> SaveChangesAsync(CancellationToken ct = default)
        => db.SaveChangesAsync(ct);

    public async Task BeginTransactionAsync(CancellationToken ct = default)
        => _tx = await db.Database.BeginTransactionAsync(ct);

    public async Task CommitTransactionAsync(CancellationToken ct = default)
    {
        if (_tx is not null) { await _tx.CommitAsync(ct); await _tx.DisposeAsync(); _tx = null; }
    }

    public async Task RollbackTransactionAsync(CancellationToken ct = default)
    {
        if (_tx is not null) { await _tx.RollbackAsync(ct); await _tx.DisposeAsync(); _tx = null; }
    }
}
