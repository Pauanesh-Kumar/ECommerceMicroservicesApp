using FluentValidation;
using MediatR;
using Microsoft.Extensions.Logging;
using UserService.Application.Interfaces;
using UserService.Domain.Common;
using UserService.Domain.Errors;
using UserService.Domain.Repositories;

namespace UserService.Application.Features.Users.Commands.UpdateUser;

public sealed record UpdateUserCommand(
    Guid    UserId,
    string  FirstName,
    string  LastName,
    string? PhoneNumber) : IRequest<Result<UpdateUserResult>>;

public sealed record UpdateUserResult(Guid UserId, string FullName, DateTime UpdatedAt);

public sealed class UpdateUserCommandValidator : AbstractValidator<UpdateUserCommand>
{
    public UpdateUserCommandValidator()
    {
        RuleFor(x => x.UserId).NotEmpty().WithMessage("UserId is required.");
        RuleFor(x => x.FirstName).NotEmpty().MaximumLength(100);
        RuleFor(x => x.LastName).NotEmpty().MaximumLength(100);
        RuleFor(x => x.PhoneNumber)
            .Matches(@"^\+?[1-9]\d{1,14}$")
            .When(x => !string.IsNullOrWhiteSpace(x.PhoneNumber))
            .WithMessage("Phone number must be in E.164 format.");
    }
}

public sealed class UpdateUserCommandHandler(
    IUserRepository                   userRepository,
    IUnitOfWork                       unitOfWork,
    IUserCacheService                 cache,
    IUserEventPublisher               eventPublisher,
    ILogger<UpdateUserCommandHandler> logger)
    : IRequestHandler<UpdateUserCommand, Result<UpdateUserResult>>
{
    public async Task<Result<UpdateUserResult>> Handle(UpdateUserCommand cmd, CancellationToken ct)
    {
        var user = await userRepository.GetByIdAsync(cmd.UserId, ct);
        if (user is null)
            return Result<UpdateUserResult>.Failure(UserErrors.NotFound);

        user.UpdateProfile(cmd.FirstName, cmd.LastName, cmd.PhoneNumber);

        await userRepository.UpdateAsync(user, ct);
        await unitOfWork.SaveChangesAsync(ct);

        // Invalidate cache so next read reflects fresh data
        await cache.RemoveAsync(cmd.UserId, ct);
        await cache.RemoveByEmailAsync(user.Email, ct);

        await eventPublisher.PublishUserUpdatedAsync(
            user.Id, user.Email, user.FirstName, user.LastName, user.PhoneNumber, ct);

        logger.LogInformation("User profile updated — Id={UserId}", cmd.UserId);
        return Result<UpdateUserResult>.Success(new UpdateUserResult(user.Id, user.FullName, user.UpdatedAt));
    }
}
