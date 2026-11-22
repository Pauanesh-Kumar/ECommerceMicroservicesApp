namespace ECommerce.Shared.Constants;

/// <summary>
/// Kafka topic names (centralized for consistency).
/// </summary>
public static class KafkaTopics
{
    public const string OrderEvents = "orders.events";
    public const string PaymentEvents = "payments.events";
    public const string ProductEvents = "products.events";
    public const string CartEvents = "carts.events";
    public const string NotificationEvents = "notifications.events";
    public const string AnalyticsEvents = "analytics.events";

    // Dead Letter Queue
    public const string OrderEventsDLQ = "orders.events.dlq";
    public const string PaymentEventsDLQ = "payments.events.dlq";
    public const string ProductEventsDLQ = "products.events.dlq";
}
