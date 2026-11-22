namespace UserService.Application.Interfaces;
/// <summary>
/// SOLID — Interface Segregation Principle:
/// Each interface below covers exactly ONE capability.
/// Application layer depends on abstractions, never on concrete implementations.
/// Infrastructure layer provides the concrete classes.
/// </summary>

// ── Password hashing ──────────────────────────────────────────────────────────
/// <summary>
/// Abstracts BCrypt (or Argon2 etc.) from Application logic.
/// Swap the algorithm without touching a single handler.
/// </summary>
public interface IPasswordHasher
{
    string Hash(string plainText);
    bool Verify(string plainText, string hash);
}
