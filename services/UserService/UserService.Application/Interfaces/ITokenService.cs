namespace UserService.Application.Interfaces;
/// <summary>
/// SOLID — Interface Segregation Principle:
/// Each interface covers exactly ONE capability.
/// Application layer depends on abstractions, never on concrete implementations.
/// Infrastructure layer provides the concrete classes.
/// </summary>

// ── JWT token generation ──────────────────────────────────────────────────────
public interface ITokenService
{
    string GenerateAccessToken(Guid userId, string email, string fullName, string status, string role);
    string GenerateRefreshToken();
    DateTime AccessTokenExpiresAt();
}