using UserService.Application.Interfaces;

namespace UserService.Infrastructure.Security;

/// <summary>
/// BCrypt implementation of IPasswordHasher.
///
/// SOLID — Dependency Inversion: Application depends on IPasswordHasher abstraction.
/// This concrete class lives in Infrastructure and is injected via DI.
/// Swap to Argon2 or PBKDF2 by replacing only this class — no Application changes.
/// </summary>
public sealed class BcryptPasswordHasher : IPasswordHasher
{
    private const int WorkFactor = 12; // OWASP recommended minimum for BCrypt

    public string Hash(string plainText)
    {
        if (string.IsNullOrWhiteSpace(plainText))
            throw new ArgumentException("Plain text password cannot be empty.", nameof(plainText));

        return BCrypt.Net.BCrypt.HashPassword(plainText, workFactor: WorkFactor);
    }

    public bool Verify(string plainText, string hash)
    {
        if (string.IsNullOrWhiteSpace(plainText) || string.IsNullOrWhiteSpace(hash))
            return false;

        return BCrypt.Net.BCrypt.Verify(plainText, hash);
    }
}
