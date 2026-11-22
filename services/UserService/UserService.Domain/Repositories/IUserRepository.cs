using UserService.Domain.Entities;

namespace UserService.Domain.Repositories;

public interface IUserRepository
{
    Task<User?>              GetByIdAsync(Guid id,       CancellationToken ct = default);
    Task<User?>              GetByEmailAsync(string email, CancellationToken ct = default);
    Task<IReadOnlyList<User>> GetPagedAsync(int page, int pageSize, string? statusFilter, CancellationToken ct = default);
    Task<int>                CountAsync(string? statusFilter, CancellationToken ct = default);
    Task                     AddAsync(User user,         CancellationToken ct = default);
    Task                     UpdateAsync(User user,      CancellationToken ct = default);
    Task<bool>               ExistsByEmailAsync(string email, CancellationToken ct = default);
}


