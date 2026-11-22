using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Threading;
using System.Threading.Tasks;
using UserService.Application.Features.Auth.Commands.ExternalLogin;

namespace UserService.Infrastructure.Security;

public sealed class TwitterOAuthProvider : IExternalOAuthProvider
{
    public string ProviderKey => "twitter";

    private readonly IHttpClientFactory _http;
    private readonly ILogger<TwitterOAuthProvider> _logger;
    private readonly string _clientId;
    private readonly string _clientSecret;

    public TwitterOAuthProvider(IHttpClientFactory http, ILogger<TwitterOAuthProvider> logger, IConfiguration config)
    {
        _http = http;
        _logger = logger;
        _clientId = config["ExternalAuth:Twitter:ClientId"] ?? string.Empty;
        _clientSecret = config["ExternalAuth:Twitter:ClientSecret"] ?? string.Empty;
    }

    public async Task<ExternalUserInfo> ValidateAsync(ExternalTokenType tokenType, string token, string? redirectUri = null, string? codeVerifier = null, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(token))
            throw new ExternalOAuthException("Token is required.");

        if (tokenType == ExternalTokenType.IdToken)
        {
            var clientB = _http.CreateClient("twitter");
            clientB.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
            using var respB = await clientB.GetAsync("https://api.twitter.com/2/users/me?user.fields=id,name,username", ct);
            var bodyB = await respB.Content.ReadAsStringAsync(ct);
            if (!respB.IsSuccessStatusCode)
                throw new ExternalOAuthException($"Twitter token validation failed ({(int)respB.StatusCode}).");

            using var docB = JsonDocument.Parse(bodyB);
            if (!docB.RootElement.TryGetProperty("data", out var data))
                throw new ExternalOAuthException("Invalid Twitter response.");

            var id = data.TryGetProperty("id", out var i) ? i.GetString() : null;
            var name = data.TryGetProperty("name", out var n) ? n.GetString() : null;
            var username = data.TryGetProperty("username", out var u) ? u.GetString() : null;
            return new ExternalUserInfo(ProviderKey, id ?? string.Empty, null, null, null, name ?? username);
        }

        if (tokenType == ExternalTokenType.AuthorizationCode)
        {
            if (string.IsNullOrWhiteSpace(redirectUri))
                throw new ExternalOAuthException("redirectUri is required for authorization_code exchange.");

            var client = _http.CreateClient();
            var parameters = new Dictionary<string, string>
            {
                ["code"] = token,
                ["grant_type"] = "authorization_code",
                ["client_id"] = _clientId,
                ["redirect_uri"] = redirectUri
            };

            // Add code_verifier for PKCE support if provided
            if (!string.IsNullOrWhiteSpace(codeVerifier))
            {
                parameters["code_verifier"] = codeVerifier;
            }

            var request = new HttpRequestMessage(HttpMethod.Post, "https://api.twitter.com/2/oauth2/token")
            {
                Content = new FormUrlEncodedContent(parameters)
            };

            if (!string.IsNullOrWhiteSpace(_clientSecret))
            {
                var basic = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{_clientId}:{_clientSecret}"));
                request.Headers.Authorization = new AuthenticationHeaderValue("Basic", basic);
            }

            using var resp = await client.SendAsync(request, ct);
            var body = await resp.Content.ReadAsStringAsync(ct);
            if (!resp.IsSuccessStatusCode)
                throw new ExternalOAuthException($"Twitter code exchange failed ({(int)resp.StatusCode}).");

            using var doc = JsonDocument.Parse(body);
            var accessToken = doc.RootElement.TryGetProperty("access_token", out var at) ? at.GetString() : null;
            if (string.IsNullOrWhiteSpace(accessToken))
                throw new ExternalOAuthException("Twitter token exchange returned no access_token.");

            var client2 = _http.CreateClient("twitter");
            client2.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            using var uresp = await client2.GetAsync("https://api.twitter.com/2/users/me?user.fields=id,name,username", ct);
            var ubody = await uresp.Content.ReadAsStringAsync(ct);
            if (!uresp.IsSuccessStatusCode)
                throw new ExternalOAuthException($"Twitter userinfo failed ({(int)uresp.StatusCode}).");

            using var udoc = JsonDocument.Parse(ubody);
            if (!udoc.RootElement.TryGetProperty("data", out var data))
                throw new ExternalOAuthException("Invalid Twitter response.");

            var id = data.TryGetProperty("id", out var i2) ? i2.GetString() : null;
            var name2 = data.TryGetProperty("name", out var n2) ? n2.GetString() : null;
            var username2 = data.TryGetProperty("username", out var u2) ? u2.GetString() : null;
            return new ExternalUserInfo(ProviderKey, id ?? string.Empty, null, null, null, name2 ?? username2);
        }

        throw new ExternalOAuthException("Unsupported token type for Twitter.");
    }
}