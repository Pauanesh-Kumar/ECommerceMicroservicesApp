using MediatR;
using UserService.Application.Features.Users.Queries.GetUser;
using UserService.Domain.Common;
using UserService.Domain.Repositories;
using FluentValidation;

namespace UserService.Application.Features.Users.Queries.GetUsers;

public sealed record GetUsersPagedQuery(
    int Page = 1,
    int PageSize = 20,
    string? StatusFilter = null) : IRequest<Result<PagedResult<UserDto>>>;

public sealed class GetUsersPagedQueryValidator : AbstractValidator<GetUsersPagedQuery>
{
    public GetUsersPagedQueryValidator()
    {
        RuleFor(x => x.Page).GreaterThanOrEqualTo(1).WithMessage("Page must be at least 1.");
        RuleFor(x => x.PageSize)
            .GreaterThanOrEqualTo(1).WithMessage("Page size must be at least 1.")
            .LessThanOrEqualTo(100).WithMessage("Page size cannot exceed 100.");
    }
}

public sealed class GetUsersPagedQueryHandler(IUserRepository repo)
    : IRequestHandler<GetUsersPagedQuery, Result<PagedResult<UserDto>>>
{
    public async Task<Result<PagedResult<UserDto>>> Handle(GetUsersPagedQuery q, CancellationToken ct)
    {
        var users = await repo.GetPagedAsync(q.Page, q.PageSize, q.StatusFilter, ct);
        var total = await repo.CountAsync(q.StatusFilter, ct);
        var pages = (int)Math.Ceiling(total / (double)q.PageSize);
        var dtos = users.Select(UserMapper.ToDto).ToList();

        var result = new PagedResult<UserDto>(
            Items: dtos,
            TotalCount: total,
            Page: q.Page,
            PageSize: q.PageSize,
            TotalPages: pages,
            HasNextPage: q.Page < pages,
            HasPreviousPage: q.Page > 1);

        return Result<PagedResult<UserDto>>.Success(result);
    }
}