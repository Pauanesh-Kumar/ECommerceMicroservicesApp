using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using UserService.Application.Interfaces;

namespace UserService.Infrastructure.Security;

/// <summary>
/// JWT implementation of ITokenService.
/// Reads Jwt:Key, Jwt:Issuer, Jwt:Audience from IConfiguration.
/// Env vars (Jwt__Key etc.) override appsettings automatically.
/// </summary>
public sealed class JwtTokenService(IConfiguration config) : ITokenService
{
    private readonly string _key = config["Jwt:Key"] ?? throw new InvalidOperationException("Jwt:Key not configured.");
    private readonly string _issuer = config["Jwt:Issuer"] ?? "userservice";
    private readonly string _audience = config["Jwt:Audience"] ?? "ecommerce-api";
    private readonly int _expiryHours = int.TryParse(config["Jwt:ExpiryHours"], out var h) ? h : 8;

    public string GenerateAccessToken(Guid userId, string email, string fullName, string status, string role)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_key));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub,   userId.ToString()),
            new Claim(JwtRegisteredClaimNames.Email, email),
            new Claim(JwtRegisteredClaimNames.Jti,   Guid.NewGuid().ToString()),
            new Claim(JwtRegisteredClaimNames.Iat,
                      DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(),
                      ClaimValueTypes.Integer64),
            new Claim(ClaimTypes.Name,               fullName),
            new Claim("status",                      status),
            new Claim(ClaimTypes.Role,                role)   // NEW — read by RequireRole(...) policies
        };

        var token = new JwtSecurityToken(
            issuer: _issuer,
            audience: _audience,
            claims: claims,
            notBefore: DateTime.UtcNow,
            expires: AccessTokenExpiresAt(),
            signingCredentials: creds);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public string GenerateRefreshToken()
    {
        // Cryptographically random — store hashed in DB for rotation (not implemented here)
        return Convert.ToBase64String(System.Security.Cryptography.RandomNumberGenerator.GetBytes(64));
    }

    public DateTime AccessTokenExpiresAt() => DateTime.UtcNow.AddHours(_expiryHours);
}