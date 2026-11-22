using FluentValidation;
using MediatR;
using UserService.Application.Interfaces;
using UserService.Domain.Common;
using UserService.Domain.Errors;
using UserService.Domain.Repositories;

namespace UserService.Application.Features.Users.Commands.AssignUserRole;

public sealed record AssignUserRoleCommand(Guid UserId, Guid RoleId) : IRequest<Result>;   // was IRequest<Result<Unit>>

public sealed class AssignUserRoleCommandValidator : AbstractValidator<AssignUserRoleCommand>
{
    public AssignUserRoleCommandValidator()
    {
        RuleFor(x => x.UserId).NotEmpty();
        RuleFor(x => x.RoleId).NotEmpty();
    }
}

public sealed class AssignUserRoleCommandHandler(
    IUserRepository userRepository,
    IRoleRepository roleRepository,
    IUnitOfWork unitOfWork,
    IUserCacheService cache)
    : IRequestHandler<AssignUserRoleCommand, Result>
{
    public async Task<Result> Handle(AssignUserRoleCommand cmd, CancellationToken ct)
    {
        var role = await roleRepository.GetByIdAsync(cmd.RoleId, ct);
        if (role is null || role.IsDeleted)
            return Result.Failure(RoleErrors.NotFoundOrInactive);

        var user = await userRepository.GetByIdAsync(cmd.UserId, ct);
        if (user is null)
            return Result.Failure(UserErrors.NotFound);

        // Correct FK update
        user.AssignRole(role.Id, role);

        await userRepository.UpdateAsync(user, ct);
        await unitOfWork.SaveChangesAsync(ct);

        // Clear caches
        await cache.RemoveAsync(user.Id, ct);
        await cache.RemoveByEmailAsync(user.Email, ct);

        return Result.Success();

    }
}