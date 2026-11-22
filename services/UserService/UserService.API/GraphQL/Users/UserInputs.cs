namespace UserService.Api.GraphQL.Users;

public sealed record RegisterUserInput(string Email, string Password, string FirstName, string LastName, string? PhoneNumber, Guid RoleId);
public sealed record UpdateUserInput(string FirstName, string LastName, string? PhoneNumber);
public sealed record ChangePasswordInput(string CurrentPassword, string NewPassword);