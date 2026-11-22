namespace UserService.Domain.Errors;

/// <summary>
/// Centralised domain error definitions for Role operations.
/// </summary>
public static class RoleErrors
{
    public static readonly Error NotFoundOrInactive =
        new("ROLE_NOT_FOUND_OR_INACTIVE", "The specified role does not exist or is no longer active.");
}