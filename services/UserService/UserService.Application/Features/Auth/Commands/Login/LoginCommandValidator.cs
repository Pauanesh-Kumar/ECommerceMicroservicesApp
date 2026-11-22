using FluentValidation;

namespace UserService.Application.Features.Auth.Commands.Login;

// -- Validator -----------------------------------------------------------------
public sealed class LoginCommandValidator : AbstractValidator<LoginCommand>
{
    public LoginCommandValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress();
        RuleFor(x => x.Password).NotEmpty();
    }
}
