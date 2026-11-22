using System.Threading;
using System.Threading.Tasks;
using UserService.Application.Features.Auth.Commands.ExternalLogin;

namespace UserService.Infrastructure.Security;

public interface IExternalOAuthProvider
{
    string ProviderKey { get; }

    Task<ExternalUserInfo> ValidateAsync(
        ExternalTokenType tokenType,
        string token,
        string? redirectUri = null,
        string? codeVerifier = null,
        CancellationToken ct = default);
}
