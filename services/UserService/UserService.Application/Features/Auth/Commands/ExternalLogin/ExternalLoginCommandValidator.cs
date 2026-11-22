using FluentValidation;

namespace UserService.Application.Features.Auth.Commands.ExternalLogin;

public sealed class ExternalLoginCommandValidator : AbstractValidator<ExternalLoginCommand>
{
    public ExternalLoginCommandValidator()
    {
        RuleFor(x => x.Provider)
            .NotEmpty().WithMessage("Provider is required (google, facebook, twitter).");

        RuleFor(x => x.Token)
            .NotEmpty().WithMessage("Token is required.");

        RuleFor(x => x.RedirectUri)
            .NotEmpty()
            .When(x => x.TokenType == ExternalTokenType.AuthorizationCode)
            .WithMessage("redirectUri is required for AuthorizationCode flow.");
    }
}
