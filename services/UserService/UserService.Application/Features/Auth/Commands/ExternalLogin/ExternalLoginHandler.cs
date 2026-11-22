using AutoMapper.Features;
using MediatR;
using Microsoft.Extensions.Logging;
using UserService.Application.Interfaces;
using UserService.Domain.Common;
using UserService.Domain.Entities;
using UserService.Domain.Errors;
using UserService.Domain.Repositories;

namespace UserService.Application.Features.Auth.Commands.ExternalLogin;

public sealed class ExternalLoginHandler(
    IExternalOAuthService externalAuthService,
    IUserRepository userRepository,
    IPasswordHasher passwordHasher,
    ITokenService tokenService,
    ILogger<ExternalLoginHandler> logger)
    : IRequestHandler<ExternalLoginCommand, Result<ExternalLoginResult>>
{
    public async Task<Result<ExternalLoginResult>> Handle(ExternalLoginCommand cmd, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(cmd.Provider) || string.IsNullOrWhiteSpace(cmd.Token))
            return Result<ExternalLoginResult>.Failure(UserErrors.ExternalProviderMissing);

        ExternalUserInfo ext;
        try
        {
            ext = await externalAuthService.ValidateAsync(
                cmd.Provider, cmd.TokenType, cmd.Token, cmd.RedirectUri, cmd.CodeVerifier, ct);
        }
        catch (ExternalOAuthException ex)
        {
            logger.LogWarning(ex, "External auth validation failed for provider {Provider}", cmd.Provider);
            return Result<ExternalLoginResult>.Failure(UserErrors.ExternalAuthFailed);
        }

        if (ext is null || string.IsNullOrWhiteSpace(ext.Email))
            return Result<ExternalLoginResult>.Failure(UserErrors.ExternalProviderNoEmail);

        var user = await userRepository.GetByEmailAsync(ext.Email, ct);
        if (user is null)
        {
            var randomPassword = Guid.NewGuid().ToString("N");
            var hashed = passwordHasher.Hash(randomPassword);

            user = User.Create(ext.Email, hashed, ext.FirstName ?? ext.FullName ?? "External", ext.LastName ?? string.Empty);
            await userRepository.AddAsync(user, ct);
        }
        else
        {
            if (user.Status == UserStatus.Inactive)
                return Result<ExternalLoginResult>.Failure(UserErrors.AccountInactive);
            if (user.Status == UserStatus.Suspended)
                return Result<ExternalLoginResult>.Failure(UserErrors.AccountSuspended);
        }

        var accessToken = tokenService.GenerateAccessToken(user.Id, user.Email, user.FullName, user.Status.ToString(), user.Role.Name);
        var refreshToken = tokenService.GenerateRefreshToken();
        var expiresAt = tokenService.AccessTokenExpiresAt();

        logger.LogInformation("External login successful for {Email} via {Provider}", user.Email, cmd.Provider);

        return Result<ExternalLoginResult>.Success(new ExternalLoginResult(
            accessToken, refreshToken, expiresAt, user.Id, user.Email, user.FullName));
    }
}