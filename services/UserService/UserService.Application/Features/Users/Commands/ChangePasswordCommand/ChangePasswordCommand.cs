using FluentValidation;
using MediatR;
using Microsoft.Extensions.Logging;
using UserService.Application.Interfaces;
using UserService.Domain.Common;
using UserService.Domain.Errors;
using UserService.Domain.Repositories;

namespace UserService.Application.Features.Users.Commands.ChangePassword;

public sealed record ChangePasswordCommand(
    Guid UserId,
    string CurrentPassword,
    string NewPassword) : IRequest<Result>;

public sealed class ChangePasswordCommandValidator : AbstractValidator<ChangePasswordCommand>
{
    public ChangePasswordCommandValidator()
    {
        RuleFor(x => x.UserId).NotEmpty();

        RuleFor(x => x.CurrentPassword).NotEmpty().WithMessage("Current password is required.");

        RuleFor(x => x.NewPassword)
            .NotEmpty()
            .MinimumLength(8)
            .Matches("[A-Z]").WithMessage("New password must contain at least one uppercase letter.")
            .Matches("[0-9]").WithMessage("New password must contain at least one digit.")
            .Matches("[^a-zA-Z0-9]").WithMessage("New password must contain at least one special character.")
            .NotEqual(x => x.CurrentPassword).WithMessage("New password must differ from current password.");
    }
}

public sealed class ChangePasswordCommandHandler(
    IUserRepository userRepository,
    IUnitOfWork unitOfWork,
    IPasswordHasher passwordHasher,
    IUserEventPublisher eventPublisher,
    ILogger<ChangePasswordCommandHandler> logger)
    : IRequestHandler<ChangePasswordCommand, Result>
{
    public async Task<Result> Handle(ChangePasswordCommand cmd, CancellationToken ct)
    {
        var user = await userRepository.GetByIdAsync(cmd.UserId, ct);
        if (user is null)
            return Result.Failure(UserErrors.NotFound);

        // Verify current password before allowing change
        if (!passwordHasher.Verify(cmd.CurrentPassword, user.PasswordHash))
            return Result.Failure(UserErrors.WrongPassword);

        var newHash = passwordHasher.Hash(cmd.NewPassword);
        user.ChangePassword(newHash);

        await userRepository.UpdateAsync(user, ct);
        await unitOfWork.SaveChangesAsync(ct);

        // Non-critical notification — publish only; NotificationService owns delivery
        // (email/SMS) and template content. Kafka failure inside PublishUserPasswordChangedAsync
        // is already swallowed/logged by UserEventPublisher — it never throws — so no try/catch
        // is needed here, unlike the old direct SMTP call this replaces.
        await eventPublisher.PublishUserPasswordChangedAsync(
            user.Id, user.Email, user.FirstName, user.LastName, user.PhoneNumber, ct);

        logger.LogInformation("Password changed — Id={UserId}", cmd.UserId);
        return Result.Success();
    }
}