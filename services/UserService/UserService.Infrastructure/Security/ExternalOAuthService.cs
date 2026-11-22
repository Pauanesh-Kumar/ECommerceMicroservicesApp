using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UserService.Application.Features.Auth.Commands.ExternalLogin;
using UserService.Application.Interfaces;

namespace UserService.Infrastructure.Security;

public sealed class ExternalOAuthService : IExternalOAuthService
{
    private readonly Dictionary<string, IExternalOAuthProvider> _map;

    public ExternalOAuthService(IEnumerable<IExternalOAuthProvider> providers)
    {
        _map = providers.ToDictionary(p => p.ProviderKey, StringComparer.OrdinalIgnoreCase);
    }

    public Task<ExternalUserInfo> ValidateAsync(
        string providerKey,
        ExternalTokenType tokenType,
        string token,
        string? redirectUri = null,
        string? codeVerifier = null,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(providerKey))
            throw new ExternalOAuthException("Provider key is required.");

        if (!_map.TryGetValue(providerKey, out var provider))
            throw new ExternalOAuthException($"Unknown external oauth provider: {providerKey}");

        return provider.ValidateAsync(tokenType, token, redirectUri, codeVerifier, ct);
    }
}