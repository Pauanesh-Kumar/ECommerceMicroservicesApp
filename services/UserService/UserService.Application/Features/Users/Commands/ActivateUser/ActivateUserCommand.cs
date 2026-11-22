using FluentValidation;
using MediatR;
using Microsoft.Extensions.Logging;
using UserService.Application.Interfaces;
using UserService.Domain.Common;
using UserService.Domain.Errors;
using UserService.Domain.Repositories;

namespace UserService.Application.Features.Users.Commands.ActivateUser;

public sealed record ActivateUserCommand(Guid UserId) : IRequest<Result>;

public sealed class ActivateUserCommandValidator : AbstractValidator<ActivateUserCommand>
{
    public ActivateUserCommandValidator() =>
        RuleFor(x => x.UserId).NotEmpty().WithMessage("UserId is required.");
}

public sealed class ActivateUserCommandHandler(
    IUserRepository                     userRepository,
    IUnitOfWork                         unitOfWork,
    IUserCacheService                   cache,
    ILogger<ActivateUserCommandHandler> logger)
    : IRequestHandler<ActivateUserCommand, Result>
{
    public async Task<Result> Handle(ActivateUserCommand cmd, CancellationToken ct)
    {
        var user = await userRepository.GetByIdAsync(cmd.UserId, ct);
        if (user is null)
            return Result.Failure(UserErrors.NotFound);

        if (user.Status == Domain.Entities.UserStatus.Active)
            return Result.Failure(UserErrors.AlreadyActive);

        user.Activate();

        await userRepository.UpdateAsync(user, ct);
        await unitOfWork.SaveChangesAsync(ct);

        await cache.RemoveAsync(cmd.UserId, ct);
        await cache.RemoveByEmailAsync(user.Email, ct);

        logger.LogInformation("User activated — Id={UserId}", cmd.UserId);
        return Result.Success();
    }
}
