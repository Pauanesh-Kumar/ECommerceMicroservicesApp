using MediatR;
using Microsoft.Extensions.Logging;
using UserService.Application.Interfaces;
using UserService.Domain.Common;
using UserService.Domain.Entities;
using UserService.Domain.Errors;
using UserService.Domain.Repositories;

namespace UserService.Application.Features.Users.Queries.GetUser;

// -- DTOs ----------------------------------------------------------------------
public sealed record UserDto(
    Guid     UserId,
    string   Email,
    string   FirstName,
    string   LastName,
    string   FullName,
    string?  PhoneNumber,
    string   Status,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    Guid RoleId,
    Role Role);

public sealed record PagedResult<T>(
    IReadOnlyList<T> Items,
    int  TotalCount,
    int  Page,
    int  PageSize,
    int  TotalPages,
    bool HasNextPage,
    bool HasPreviousPage);

// -- Queries -------------------------------------------------------------------
public sealed record GetUserByIdQuery(Guid UserId)     : IRequest<Result<UserDto>>;
public sealed record GetUserByEmailQuery(string Email) : IRequest<Result<UserDto>>;

// -- Handlers ------------------------------------------------------------------
public sealed class GetUserByIdQueryHandler(
    IUserRepository                  userRepository,
    IUserCacheService                cache,
    ILogger<GetUserByIdQueryHandler> logger)
    : IRequestHandler<GetUserByIdQuery, Result<UserDto>>
{
    public async Task<Result<UserDto>> Handle(GetUserByIdQuery q, CancellationToken ct)
    {
        // 1. Try cache first (cache-aside pattern)
        var cached = await cache.GetByIdAsync(q.UserId, ct);
        if (cached is not null)
        {
            logger.LogDebug("Cache HIT — userId={UserId}", q.UserId);
            return Result<UserDto>.Success(CacheEntryToDto(cached));
        }

        // 2. Fall back to database
        var user = await userRepository.GetByIdAsync(q.UserId, ct);
        if (user is null)
            return Result<UserDto>.Failure(UserErrors.NotFound);

        var dto = UserMapper.ToDto(user);

        // 3. Populate cache for subsequent reads
        await cache.SetAsync(UserMapper.ToCacheEntry(user), ct);

        return Result<UserDto>.Success(dto);
    }

    private static UserDto CacheEntryToDto(UserCacheEntry c) => new(
        c.UserId, c.Email, c.FirstName, c.LastName, c.FullName,
        c.PhoneNumber, c.Status, c.CreatedAt, c.UpdatedAt,c.RoleId, c.Role);
}

public sealed class GetUserByEmailQueryHandler(
    IUserRepository                     userRepository,
    IUserCacheService                   cache,
    ILogger<GetUserByEmailQueryHandler> logger)
    : IRequestHandler<GetUserByEmailQuery, Result<UserDto>>
{
    public async Task<Result<UserDto>> Handle(GetUserByEmailQuery q, CancellationToken ct)
    {
        var cached = await cache.GetByEmailAsync(q.Email, ct);
        if (cached is not null)
        {
            logger.LogDebug("Cache HIT — email={Email}", q.Email);
            return Result<UserDto>.Success(new UserDto(
                cached.UserId, cached.Email, cached.FirstName, cached.LastName,
                cached.FullName, cached.PhoneNumber, cached.Status,
                cached.CreatedAt, cached.UpdatedAt, cached.RoleId, cached.Role));
        }

        var user = await userRepository.GetByEmailAsync(q.Email, ct);
        if (user is null)
            return Result<UserDto>.Failure(UserErrors.EmailNotFound);

        var dto = UserMapper.ToDto(user);
        await cache.SetAsync(UserMapper.ToCacheEntry(user), ct);

        return Result<UserDto>.Success(dto);
    }
}

// -- Shared mapper — internal so GetUsersQuery can also use it -----------------
internal static class UserMapper
{
    public static UserDto ToDto(User u) => new(
        u.Id, u.Email, u.FirstName, u.LastName, u.FullName,
        u.PhoneNumber, u.Status.ToString(), u.CreatedAt, u.UpdatedAt,u.RoleId, u.Role);

    public static UserCacheEntry ToCacheEntry(User u) => new(
        u.Id, u.Email, u.FirstName, u.LastName, u.FullName,
        u.PhoneNumber, u.Status.ToString(), u.CreatedAt, u.UpdatedAt, u.RoleId, u.Role);
}
