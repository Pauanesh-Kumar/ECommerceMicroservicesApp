using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using Google.Apis.Auth;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Threading;
using System.Threading.Tasks;
using UserService.Application.Features.Auth.Commands.ExternalLogin;

namespace UserService.Infrastructure.Security;

public sealed class GoogleOAuthProvider : IExternalOAuthProvider
{
    public string ProviderKey => "google";

    private readonly IHttpClientFactory _http;
    private readonly ILogger<GoogleOAuthProvider> _logger;
    private readonly string _clientId;
    private readonly string _clientSecret;

    public GoogleOAuthProvider(IHttpClientFactory http, ILogger<GoogleOAuthProvider> logger, IConfiguration config)
    {
        _http = http;
        _logger = logger;
        _clientId = config["ExternalAuth:Google:ClientId"] ?? string.Empty;
        _clientSecret = config["ExternalAuth:Google:ClientSecret"] ?? string.Empty;
    }

    public async Task<ExternalUserInfo> ValidateAsync(ExternalTokenType tokenType, string token, string? redirectUri = null, string? codeVerifier = null, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(token))
            throw new ExternalOAuthException("Token is required.");

        if (tokenType == ExternalTokenType.IdToken)
        {
            try
            {
                var settings = new GoogleJsonWebSignature.ValidationSettings();
                if (!string.IsNullOrWhiteSpace(_clientId))
                    settings.Audience = new[] { _clientId };

                // GoogleJsonWebSignature.ValidateAsync does not accept CancellationToken as the 3rd parameter in many versions.
                // Call the overload that accepts (string, ValidationSettings) to avoid type mismatch.
                var payload = await GoogleJsonWebSignature.ValidateAsync(token, settings);
                return new ExternalUserInfo(ProviderKey, payload.Subject ?? string.Empty, payload.Email, payload.GivenName, payload.FamilyName, payload.Name);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Google ID token validation failed");
                throw new ExternalOAuthException("Google ID token validation failed.", ex);
            }
        }

        if (tokenType == ExternalTokenType.AuthorizationCode)
        {
            if (string.IsNullOrWhiteSpace(redirectUri))
                throw new ExternalOAuthException("redirectUri is required for authorization_code exchange.");

            var client = _http.CreateClient();
            var body = new Dictionary<string, string>
            {
                ["code"] = token,
                ["client_id"] = _clientId,
                ["client_secret"] = _clientSecret,
                ["redirect_uri"] = redirectUri,
                ["grant_type"] = "authorization_code"
            };

            // Add code_verifier for PKCE support if provided
            if (!string.IsNullOrWhiteSpace(codeVerifier))
            {
                body["code_verifier"] = codeVerifier;
            }

            using var resp = await client.PostAsync("https://oauth2.googleapis.com/token", new FormUrlEncodedContent(body), ct);
            var bodyText = await resp.Content.ReadAsStringAsync(ct);
            if (!resp.IsSuccessStatusCode)
            {
                _logger.LogWarning("Google token exchange failed: {Status} {Body}", resp.StatusCode, bodyText);
                throw new ExternalOAuthException($"Google code exchange failed ({(int)resp.StatusCode}).");
            }

            using var doc = JsonDocument.Parse(bodyText);
            var root = doc.RootElement;
            var idToken = root.TryGetProperty("id_token", out var idt) ? idt.GetString() : null;
            var accessToken = root.TryGetProperty("access_token", out var at) ? at.GetString() : null;

            if (!string.IsNullOrWhiteSpace(idToken))
            {
                try
                {
                    var settings = new GoogleJsonWebSignature.ValidationSettings();
                    if (!string.IsNullOrWhiteSpace(_clientId))
                        settings.Audience = new[] { _clientId };

                    var pay = await GoogleJsonWebSignature.ValidateAsync(idToken, settings);
                    return new ExternalUserInfo(ProviderKey, pay.Subject ?? string.Empty, pay.Email, pay.GivenName, pay.FamilyName, pay.Name);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Google id_token validation after exchange failed");
                    throw new ExternalOAuthException("Google id_token validation failed.", ex);
                }
            }

            if (!string.IsNullOrWhiteSpace(accessToken))
            {
                var client2 = _http.CreateClient("google");
                client2.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
                using var uresp = await client2.GetAsync("https://www.googleapis.com/oauth2/v3/userinfo", ct);
                var ubody = await uresp.Content.ReadAsStringAsync(ct);
                if (!uresp.IsSuccessStatusCode)
                {
                    _logger.LogWarning("Google userinfo failed after exchange: {Status} {Body}", uresp.StatusCode, ubody);
                    throw new ExternalOAuthException("Google userinfo failed after token exchange.");
                }

                using var udoc = JsonDocument.Parse(ubody);
                var r = udoc.RootElement;
                var sub = r.TryGetProperty("sub", out var s) ? s.GetString() : null;
                var email = r.TryGetProperty("email", out var e) ? e.GetString() : null;
                var name = r.TryGetProperty("name", out var n) ? n.GetString() : null;
                var given = r.TryGetProperty("given_name", out var g) ? g.GetString() : null;
                var family = r.TryGetProperty("family_name", out var f) ? f.GetString() : null;

                return new ExternalUserInfo(ProviderKey, sub ?? string.Empty, email, given, family, name);
            }

            throw new ExternalOAuthException("Google token exchange succeeded but no usable token returned.");
        }

        throw new ExternalOAuthException("Unsupported token type for Google.");
    }
}