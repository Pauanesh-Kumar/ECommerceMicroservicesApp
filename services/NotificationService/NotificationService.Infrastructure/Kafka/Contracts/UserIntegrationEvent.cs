using System.Text.Json.Serialization;

namespace NotificationService.Infrastructure.Kafka.Contracts;

/// <summary>
/// Discriminated envelope published by UserService on the "user-updated" topic.
/// EventType raw values: "UserRegistered" | "UserUpdated" | "UserDeactivated" etc.
/// Use EventTypeMapper.Resolve() to convert to NotificationEventType.
/// </summary>
public sealed class UserIntegrationEvent
{
    [JsonPropertyName("eventType")] public string EventType { get; init; } = string.Empty;
    [JsonPropertyName("userId")] public Guid UserId { get; init; }
    [JsonPropertyName("email")] public string? Email { get; init; }
    [JsonPropertyName("firstName")] public string? FirstName { get; init; }
    [JsonPropertyName("lastName")] public string? LastName { get; init; }
    [JsonPropertyName("phoneNumber")] public string? PhoneNumber { get; init; }
    [JsonPropertyName("occurredOn")] public DateTime OccurredOn { get; init; }
}