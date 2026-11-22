using System.Text.Json.Serialization;

namespace NotificationService.Infrastructure.Kafka.Contracts;

/// <summary>
/// Integration event envelope published by OrderService on the "order-placed" topic.
/// </summary>
public sealed class OrderIntegrationEvent
{
    [JsonPropertyName("eventType")] public string EventType { get; init; } = string.Empty;
    [JsonPropertyName("orderId")] public Guid OrderId { get; init; }
    [JsonPropertyName("userId")] public Guid UserId { get; init; }
    [JsonPropertyName("email")] public string? Email { get; init; }
    [JsonPropertyName("phoneNumber")] public string? PhoneNumber { get; init; }
    [JsonPropertyName("firstName")] public string? FirstName { get; init; }
    [JsonPropertyName("lastName")] public string? LastName { get; init; }
    [JsonPropertyName("totalAmount")] public decimal TotalAmount { get; init; }
    [JsonPropertyName("currency")] public string Currency { get; init; } = "USD";
    [JsonPropertyName("occurredOn")] public DateTime OccurredOn { get; init; }
}