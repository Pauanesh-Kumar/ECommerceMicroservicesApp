namespace UserService.Application.Contracts;

// Published by UserService to the "user.registered" Kafka topic after a successful registration.
// Must stay field-for-field identical to NotificationService's copy of this class
// (Application/Notifications/Contracts/UserRegisteredIntegrationEvent.cs) since it's the
// wire contract between the two services. Move both to a shared Contracts NuGet
// package/project if you want compile-time enforcement of that instead of a comment.
public record UserRegisteredIntegrationEvent(
    Guid UserId,
    string Email,
    string FullName,
    string? PhoneNumber,
    DateTime RegisteredAtUtc);
