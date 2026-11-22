using ECommerce.Shared.Security;
using HotChocolate.Authorization;
//using HotChocolate.Types;
using MediatR;
using StackExchange.Redis;
using UserService.Application.Features.Users.Queries.GetUser;
using UserService.Application.Features.Users.Queries.GetUsers;
using UserService.Application.Interfaces;
using UserService.Domain.Repositories;

namespace UserService.Api.GraphQL.Users;

public sealed class UserProfileDto
{
    public Guid UserId { get; init; }
    public string Email { get; init; } = default!;
    public string FullName { get; init; } = default!;
    public string? PhoneNumber { get; init; }
    public string Status { get; init; } = default!;

    public static UserProfileDto FromCacheEntry(UserCacheEntry e) => new()
    {
        UserId = e.UserId,
        Email = e.Email,
        FullName = e.FullName,
        PhoneNumber = e.PhoneNumber,
        Status = e.Status
    };
}

[ExtendObjectType(typeof(Query))]
public sealed class UserQueries
{
    /// <summary>
    /// Self-service only — returns the caller's own profile. No id parameter
    /// by design: "you can only see your own profile" is true by construction,
    /// not by a comparison that could be forgotten in a future edit.
    /// If you need an admin lookup by arbitrary id, add a SEPARATE query with
    /// its own [Authorize(Policy = "Admin")] — don't loosen this one.
    /// </summary>
    [Authorize(Policy = "User")]
    public async Task<UserProfileDto?> GetMeAsync(
    ICurrentUserAccessor currentUser,
    IUserCacheService cache,
    IUserRepository repo,
    CancellationToken ct)
    {
        if (currentUser.UserId is not { } userId) return null;

        var entry = await cache.GetByIdAsync(userId, ct);
        if (entry is not null) return UserProfileDto.FromCacheEntry(entry);

        var user = await repo.GetByIdAsync(userId, ct);
        if (user is null) return null;

        var newEntry = new UserCacheEntry(
            UserId: user.Id,
            Email: user.Email,
            FirstName: user.FirstName,
            LastName: user.LastName,
            FullName: user.FullName,
            PhoneNumber: user.PhoneNumber,
            Status: user.Status.ToString(),
            CreatedAt: user.CreatedAt,
            UpdatedAt: user.UpdatedAt,
            RoleId: user.RoleId,
            Role: user.Role
        );

        await cache.SetAsync(newEntry, ct);
        return UserProfileDto.FromCacheEntry(newEntry);
    }

    /// <summary>
    /// Admin lookup by email — mirrors REST GET /api/users/by-email/{email}.
    /// Unwraps Result&lt;UserDto&gt; — returns null on failure rather than
    /// leaking the internal Result wrapper through the GraphQL schema.
    /// </summary>
    [Authorize(Policy = "Admin")]
    public async Task<UserDto?> GetUserByEmailAsync(
        string email, ISender mediator, CancellationToken ct)
    {
        var result = await mediator.Send(new GetUserByEmailQuery(email), ct);
        return result.IsSuccess ? result.Value : null;
    }

    /// <summary>
    /// Admin paged listing — mirrors REST GET /api/users.
    /// TODO: swap `object` for the real return type once GetUsersPagedQuery's
    /// handler is shared — likely Result&lt;PagedResult&lt;UserDto&gt;&gt;.
    /// </summary>
    [Authorize(Policy = "Admin")]
    public async Task<PagedResult<UserDto>?> GetUsersAsync(
    int page, int pageSize, string? status, ISender mediator, CancellationToken ct)
    {
        var result = await mediator.Send(new GetUsersPagedQuery(page, pageSize, status), ct);
        return result.IsSuccess ? result.Value : null;
    }
}