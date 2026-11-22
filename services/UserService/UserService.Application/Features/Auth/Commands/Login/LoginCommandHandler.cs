using MediatR;
using Microsoft.Extensions.Logging;
using UserService.Application.Interfaces;
using UserService.Domain.Common;
using UserService.Domain.Errors;
using UserService.Domain.Repositories;

namespace UserService.Application.Features.Auth.Commands.Login;

public sealed class LoginCommandHandler(
    IUserRepository userRepository,
    IPasswordHasher passwordHasher,
    ITokenService tokenService,
    ILogger<LoginCommandHandler> logger)
    : IRequestHandler<LoginCommand, Result<LoginResult>>
{
    public async Task<Result<LoginResult>> Handle(LoginCommand cmd, CancellationToken ct)
    {
        // 1. Find user — same generic message for both "not found" and "wrong password"
        //    (prevents email enumeration attacks)
        // NOTE: GetByEmailAsync must eager-load Role (.Include(u => u.Role) if using EF
        // directly in the repository impl) — user.Role.Name below will null-ref otherwise.
        var user = await userRepository.GetByEmailAsync(cmd.Email, ct);
        if (user is null || !passwordHasher.Verify(cmd.Password, user.PasswordHash))
        {
            logger.LogWarning("Failed login attempt for {Email}", cmd.Email);
            return Result<LoginResult>.Failure(UserErrors.InvalidCredentials);
        }

        // 2. Guard account status
        if (user.Status == Domain.Entities.UserStatus.Inactive)
            return Result<LoginResult>.Failure(UserErrors.AccountInactive);

        if (user.Status == Domain.Entities.UserStatus.Suspended)
            return Result<LoginResult>.Failure(UserErrors.AccountSuspended);

        // 3. Generate tokens via injected ITokenService (not IConfiguration directly)
        //    Role name now passed through so it lands in the JWT as a role claim —
        //    this is what makes RequireRole("Admin")/("User") policies work at runtime.
        var accessToken = tokenService.GenerateAccessToken(
            user.Id, user.Email, user.FullName, user.Status.ToString(), user.Role.Name);
        var refreshToken = tokenService.GenerateRefreshToken();
        var expiresAt = tokenService.AccessTokenExpiresAt();

        logger.LogInformation("User logged in — Id={UserId} Role={Role}", user.Id, user.Role.Name);

        return Result<LoginResult>.Success(new LoginResult(
            accessToken, refreshToken, expiresAt, user.Id, user.Email, user.FullName));
    }
}