using System.Threading;
using System.Threading.Tasks;
using UserService.Application.Features.Auth.Commands.ExternalLogin;

namespace UserService.Application.Interfaces;

public interface IExternalOAuthService
{
    Task<ExternalUserInfo> ValidateAsync(
        string providerKey,
        ExternalTokenType tokenType,
        string token,
        string? redirectUri = null,
        string? codeVerifier = null,
        CancellationToken ct = default);
}