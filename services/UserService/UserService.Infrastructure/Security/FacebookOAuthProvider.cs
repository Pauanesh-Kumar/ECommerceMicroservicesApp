using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Threading;
using System.Threading.Tasks;
using UserService.Application.Features.Auth.Commands.ExternalLogin;

namespace UserService.Infrastructure.Security;

public sealed class FacebookOAuthProvider : IExternalOAuthProvider
{
    public string ProviderKey => "facebook";

    private readonly IHttpClientFactory _http;
    private readonly ILogger<FacebookOAuthProvider> _logger;
    private readonly string _appId;
    private readonly string _appSecret;

    public FacebookOAuthProvider(IHttpClientFactory http, ILogger<FacebookOAuthProvider> logger, IConfiguration config)
    {
        _http = http;
        _logger = logger;
        _appId = config["ExternalAuth:Facebook:AppId"] ?? string.Empty;
        _appSecret = config["ExternalAuth:Facebook:AppSecret"] ?? string.Empty;
    }

    public async Task<ExternalUserInfo> ValidateAsync(ExternalTokenType tokenType, string token, string? redirectUri = null, string? codeVerifier = null, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(token))
            throw new ExternalOAuthException("Token is required.");

        string? accessToken = null;
        if (tokenType == ExternalTokenType.AuthorizationCode)
        {
            if (string.IsNullOrWhiteSpace(_appId) || string.IsNullOrWhiteSpace(_appSecret))
                throw new ExternalOAuthException("Facebook app credentials not configured.");

            if (string.IsNullOrWhiteSpace(redirectUri))
                throw new ExternalOAuthException("redirectUri is required for Facebook code exchange.");

            var client = _http.CreateClient();

            // Build URL with required parameters
            var queryParams = new Dictionary<string, string>
            {
                ["client_id"] = _appId,
                ["client_secret"] = _appSecret,
                ["redirect_uri"] = redirectUri,
                ["code"] = token
            };

            // Add code_verifier for PKCE support if provided
            if (!string.IsNullOrWhiteSpace(codeVerifier))
            {
                queryParams["code_verifier"] = codeVerifier;
            }

            var queryString = string.Join("&", queryParams.Select(kvp => $"{kvp.Key}={Uri.EscapeDataString(kvp.Value)}"));
            var url = $"https://graph.facebook.com/v17.0/oauth/access_token?{queryString}";

            using var resp = await client.GetAsync(url, ct);
            var body = await resp.Content.ReadAsStringAsync(ct);
            if (!resp.IsSuccessStatusCode)
                throw new ExternalOAuthException($"Facebook code exchange failed ({(int)resp.StatusCode}).");

            using var doc = JsonDocument.Parse(body);
            accessToken = doc.RootElement.TryGetProperty("access_token", out var at) ? at.GetString() : null;
        }

        accessToken ??= token;

        var client2 = _http.CreateClient("facebook");
        var meUrl = $"https://graph.facebook.com/me?fields=id,first_name,last_name,name,email&access_token={Uri.EscapeDataString(accessToken)}";
        using var meResp = await client2.GetAsync(meUrl, ct);
        var meBody = await meResp.Content.ReadAsStringAsync(ct);
        if (!meResp.IsSuccessStatusCode)
            throw new ExternalOAuthException($"Facebook token validation failed ({(int)meResp.StatusCode}).");

        using var meDoc = JsonDocument.Parse(meBody);
        var root = meDoc.RootElement;
        var id = root.TryGetProperty("id", out var i) ? i.GetString() : null;
        var first = root.TryGetProperty("first_name", out var f) ? f.GetString() : null;
        var last = root.TryGetProperty("last_name", out var l) ? l.GetString() : null;
        var name = root.TryGetProperty("name", out var n) ? n.GetString() : null;
        var email = root.TryGetProperty("email", out var e) ? e.GetString() : null;

        return new ExternalUserInfo(ProviderKey, id ?? string.Empty, email, first, last, name);
    }
}