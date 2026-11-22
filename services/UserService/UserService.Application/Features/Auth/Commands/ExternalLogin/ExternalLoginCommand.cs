using MediatR;
using UserService.Domain.Common;

namespace UserService.Application.Features.Auth.Commands.ExternalLogin;

public sealed record ExternalLoginCommand(
    string Provider,
    ExternalTokenType TokenType,
    string Token,
    string? RedirectUri,
    string? CodeVerifier) : IRequest<Result<ExternalLoginResult>>;

public sealed record ExternalLoginResult(
    string AccessToken,
    string RefreshToken,
    DateTime ExpiresAt,
    Guid UserId,
    string Email,
    string FullName);