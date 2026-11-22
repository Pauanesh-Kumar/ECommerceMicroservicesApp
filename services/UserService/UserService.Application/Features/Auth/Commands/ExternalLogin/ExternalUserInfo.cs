namespace UserService.Application.Features.Auth.Commands.ExternalLogin;

public sealed record ExternalUserInfo(
    string Provider,
    string ProviderUserId,
    string? Email,
    string? FirstName,
    string? LastName,
    string? FullName);
