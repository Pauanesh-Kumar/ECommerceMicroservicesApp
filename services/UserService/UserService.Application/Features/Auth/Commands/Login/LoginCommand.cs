using MediatR;
using UserService.Domain.Common;


namespace UserService.Application.Features.Auth.Commands.Login;

// -- Command -------------------------------------------------------------------
public sealed record LoginCommand(string Email, string Password) : IRequest<Result<LoginResult>>;

public sealed record LoginResult(
    string   AccessToken,
    string   RefreshToken,
    DateTime ExpiresAt,
    Guid     UserId,
    string   Email,
    string   FullName);


