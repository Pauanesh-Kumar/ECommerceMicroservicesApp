namespace UserService.Api.GraphQL.Users;

public sealed record RegisterUserPayload(Guid? UserId, bool Success, string? ErrorCode, string? ErrorMessage);
public sealed record UpdateUserPayload(bool Success, string? ErrorCode, string? ErrorMessage);
public sealed record DeactivateUserPayload(bool Success, string? ErrorCode, string? ErrorMessage);
public sealed record ChangePasswordPayload(bool Success, string? ErrorCode, string? ErrorMessage);
public sealed record AssignRolePayload(bool Success, string? ErrorCode, string? ErrorMessage);