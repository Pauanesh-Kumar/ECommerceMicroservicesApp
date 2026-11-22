using FluentValidation;
using MediatR;
using Microsoft.Extensions.Logging;
using UserService.Application.Interfaces;
using UserService.Domain.Common;
using UserService.Domain.Entities;
using UserService.Domain.Errors;
using UserService.Domain.Repositories;

namespace UserService.Application.Features.Users.Commands.RegisterUser;

// -- Command -------------------------------------------------------------------
public sealed record RegisterUserCommand(
    string  Email,
    string  Password,
    string  FirstName,
    string  LastName,
    string? PhoneNumber,
    Guid? RoleId) : IRequest<Result<RegisterUserResult>>;

public sealed record RegisterUserResult(Guid UserId, string Email, string FullName);

// -- Validator -----------------------------------------------------------------
public sealed class RegisterUserCommandValidator : AbstractValidator<RegisterUserCommand>
{
    public RegisterUserCommandValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email is required.")
            .EmailAddress().WithMessage("A valid email address is required.")
            .MaximumLength(255);

        RuleFor(x => x.Password)
            .NotEmpty().WithMessage("Password is required.")
            .MinimumLength(8).WithMessage("Password must be at least 8 characters.")
            .Matches("[A-Z]").WithMessage("Password must contain at least one uppercase letter.")
            .Matches("[0-9]").WithMessage("Password must contain at least one digit.")
            .Matches("[^a-zA-Z0-9]").WithMessage("Password must contain at least one special character.");

        RuleFor(x => x.FirstName).NotEmpty().MaximumLength(100);
        RuleFor(x => x.LastName).NotEmpty().MaximumLength(100);

        RuleFor(x => x.PhoneNumber)
            .Matches(@"^\+?[1-9]\d{1,14}$")
            .When(x => !string.IsNullOrWhiteSpace(x.PhoneNumber))
            .WithMessage("Phone number must be in E.164 format.");
    }
}

// -- Handler -------------------------------------------------------------------
public sealed class RegisterUserCommandHandler(
    IUserRepository                     userRepository,
    IUnitOfWork                         unitOfWork,
    IPasswordHasher                     passwordHasher,
    IUserEventPublisher                 eventPublisher,
    ILogger<RegisterUserCommandHandler> logger)
    : IRequestHandler<RegisterUserCommand, Result<RegisterUserResult>>
{
    public async Task<Result<RegisterUserResult>> Handle(
        RegisterUserCommand command,
        CancellationToken   ct)
    {
        // 1. Guard: duplicate email
        if (await userRepository.ExistsByEmailAsync(command.Email, ct))
        {
            logger.LogWarning("Registration blocked — email already exists: {Email}", command.Email);
            return Result<RegisterUserResult>.Failure(UserErrors.EmailAlreadyExists);
        }

        // 2. Hash password via abstraction (BCrypt injected via IPasswordHasher)
        var hash = passwordHasher.Hash(command.Password);

        // 3. Create aggregate — domain enforces all invariants
        var user = User.Create(command.Email, hash, command.FirstName, command.LastName, command.PhoneNumber, command.RoleId);

        // 4. Persist
        await userRepository.AddAsync(user, ct);
        await unitOfWork.SaveChangesAsync(ct);

        // 5. Publish integration event (non-blocking — Kafka producer)
        await eventPublisher.PublishUserRegisteredAsync(
            user.Id, user.Email, user.FirstName, user.LastName, user.PhoneNumber, ct);

        logger.LogInformation("User registered — Id={UserId} Email={Email}", user.Id, user.Email);
        return Result<RegisterUserResult>.Success(new RegisterUserResult(user.Id, user.Email, user.FullName));
    }
}
