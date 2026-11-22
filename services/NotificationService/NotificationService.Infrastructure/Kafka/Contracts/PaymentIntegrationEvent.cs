using System.Text.Json.Serialization;

namespace NotificationService.Infrastructure.Kafka.Contracts;

/// <summary>
/// Integration event envelope published by PaymentService on the "payment-processed" topic.
/// </summary>
public sealed class PaymentIntegrationEvent
{
    [JsonPropertyName("eventType")] public string EventType { get; init; } = string.Empty;
    [JsonPropertyName("paymentId")] public Guid PaymentId { get; init; }
    [JsonPropertyName("orderId")] public Guid OrderId { get; init; }
    [JsonPropertyName("userId")] public Guid UserId { get; init; }
    [JsonPropertyName("phoneNumber")] public string? PhoneNumber { get; init; }
    [JsonPropertyName("email")] public string? Email { get; init; }
    [JsonPropertyName("firstName")] public string? FirstName { get; init; }
    [JsonPropertyName("lastName")] public string? LastName { get; init; }
    [JsonPropertyName("amount")] public decimal Amount { get; init; }
    [JsonPropertyName("currency")] public string Currency { get; init; } = "USD";
    [JsonPropertyName("success")] public bool Success { get; init; }
    [JsonPropertyName("failureReason")] public string? FailureReason { get; init; }
    [JsonPropertyName("occurredOn")] public DateTime OccurredOn { get; init; }
}