using FluentValidation;
using MediatR;
using Microsoft.Extensions.Logging;
using UserService.Application.Interfaces;
using UserService.Domain.Common;
using UserService.Domain.Errors;
using UserService.Domain.Repositories;

namespace UserService.Application.Features.Users.Commands.DeactivateUser;

public sealed record DeactivateUserCommand(Guid UserId, string? Reason) : IRequest<Result>;

public sealed class DeactivateUserCommandValidator : AbstractValidator<DeactivateUserCommand>
{
    public DeactivateUserCommandValidator()
    {
        RuleFor(x => x.UserId).NotEmpty();
        RuleFor(x => x.Reason).MaximumLength(500).When(x => x.Reason is not null);
    }
}

public sealed class DeactivateUserCommandHandler(
    IUserRepository                       userRepository,
    IUnitOfWork                           unitOfWork,
    IUserCacheService                     cache,
    IUserEventPublisher                   eventPublisher,
    ILogger<DeactivateUserCommandHandler> logger)
    : IRequestHandler<DeactivateUserCommand, Result>
{
    public async Task<Result> Handle(DeactivateUserCommand cmd, CancellationToken ct)
    {
        var user = await userRepository.GetByIdAsync(cmd.UserId, ct);
        if (user is null)
            return Result.Failure(UserErrors.NotFound);

        if (user.Status == Domain.Entities.UserStatus.Inactive)
            return Result.Failure(UserErrors.AlreadyInactive);

        user.Deactivate();

        await userRepository.UpdateAsync(user, ct);
        await unitOfWork.SaveChangesAsync(ct);

        // Evict from cache immediately
        await cache.RemoveAsync(cmd.UserId, ct);
        await cache.RemoveByEmailAsync(user.Email, ct);

        // Notify via Kafka + email (fire-and-forget errors logged, not thrown)
        await SafePublishAsync(() =>
            eventPublisher.PublishUserDeactivatedAsync(user.Id, user.Email, user.FirstName, user.LastName, user.PhoneNumber, ct), logger, "Kafka");

        logger.LogInformation("User deactivated — Id={UserId} Reason={Reason}", cmd.UserId, cmd.Reason ?? "not provided");
        return Result.Success();
    }

    /// <summary>
    /// Wraps non-critical side-effects so they never fail the main transaction.
    /// (Notification failures must NOT roll back the deactivation.)
    /// </summary>
    private static async Task SafePublishAsync(Func<Task> action, ILogger logger, string target)
    {
        try   { await action(); }
        catch (Exception ex) { logger.LogWarning(ex, "Non-critical {Target} publish failed — continuing", target); }
    }
}
