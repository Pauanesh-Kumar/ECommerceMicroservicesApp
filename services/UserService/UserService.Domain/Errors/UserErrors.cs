namespace UserService.Domain.Errors;

/// <summary>
/// Centralised domain error definitions.
/// </summary>
public static class UserErrors
{
    // ── Not found ─────────────────────────────────────────────────────────────
    public static readonly Error NotFound = new("USER_NOT_FOUND", "User was not found.");
    public static readonly Error EmailNotFound = new("USER_EMAIL_NOT_FOUND", "No user exists with that email address.");

    // ── Conflict ──────────────────────────────────────────────────────────────
    public static readonly Error EmailAlreadyExists = new("USER_EMAIL_EXISTS", "Email address is already registered.");

    // ── State violations ──────────────────────────────────────────────────────
    public static readonly Error AlreadyInactive = new("USER_ALREADY_INACTIVE", "User account is already inactive.");
    public static readonly Error AlreadyActive = new("USER_ALREADY_ACTIVE", "User account is already active.");
    public static readonly Error AccountInactive = new("USER_ACCOUNT_INACTIVE", "User account is inactive. Contact support.");
    public static readonly Error AccountSuspended = new("USER_ACCOUNT_SUSPENDED", "User account has been suspended. Contact support.");

    // ── Auth ──────────────────────────────────────────────────────────────────
    public static readonly Error InvalidCredentials = new("USER_INVALID_CREDENTIALS", "Invalid email or password.");
    public static readonly Error InvalidToken = new("USER_INVALID_TOKEN", "The provided token is invalid or expired.");

    // ── Password ──────────────────────────────────────────────────────────────
    public static readonly Error SamePassword = new("USER_SAME_PASSWORD", "New password must be different from the current password.");
    public static readonly Error WrongPassword = new("USER_WRONG_PASSWORD", "Current password is incorrect.");

    // ── External auth ─────────────────────────────────────────────────────────
    public static readonly Error ExternalProviderMissing = new("EXTERNAL_PROVIDER_MISSING", "Provider and access token are required.");
    public static readonly Error ExternalAuthFailed = new("EXTERNAL_AUTH_FAILED", "External authentication failed.");
    public static readonly Error ExternalProviderNoEmail = new("EXTERNAL_PROVIDER_NO_EMAIL", "External provider did not return an email.");
}

/// <summary>
/// Immutable error value object — carries a machine-readable code and human message.
/// </summary>
public sealed record Error(string Code, string Message)
{
    public override string ToString() => $"[{Code}] {Message}";
}