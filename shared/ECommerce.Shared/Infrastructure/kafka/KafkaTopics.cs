namespace ECommerce.Shared.Infrastructure.Kafka;

/// <summary>
/// Centralized Kafka topic constants
/// Naming: {service}.{entity}.{action}
/// DLQ: {topic}.dlq for failed messages
/// </summary>
public static class KafkaTopics
{
    // ========== ORDER TOPICS ==========
    public const string Orders_Created = "orders.created";
    public const string Orders_Updated = "orders.updated";
    public const string Orders_Cancelled = "orders.cancelled";
    public const string Orders_Created_DLQ = $"{Orders_Created}.dlq";

    // ========== PAYMENT TOPICS ==========
    public const string Payments_Authorized = "payments.authorized";
    public const string Payments_Processed = "payments.processed";
    public const string Payments_Failed = "payments.failed";
    public const string Payments_Refunded = "payments.refunded";
    public const string Payments_Processed_DLQ = $"{Payments_Processed}.dlq";

    // ========== PRODUCT TOPICS ==========
    public const string Products_InventoryUpdated = "products.inventory.updated";
    public const string Products_PriceChanged = "products.price.changed";
    public const string Products_InventoryUpdated_DLQ = $"{Products_InventoryUpdated}.dlq";

    // ========== CART TOPICS ==========
    public const string Carts_ItemAdded = "carts.item.added";
    public const string Carts_ItemRemoved = "carts.item.removed";
    public const string Carts_Cleared = "carts.cleared";

    // ========== ANALYTICS TOPICS ==========
    public const string Analytics_Events = "analytics.events";
    public const string Analytics_Events_DLQ = $"{Analytics_Events}.dlq";

    public static readonly string[] AllTopics = new[]
    {
        Orders_Created, Orders_Updated, Orders_Cancelled,
        Payments_Authorized, Payments_Processed, Payments_Failed, Payments_Refunded,
        Products_InventoryUpdated, Products_PriceChanged,
        Carts_ItemAdded, Carts_ItemRemoved, Carts_Cleared,
        Analytics_Events,
        Orders_Created_DLQ, Payments_Processed_DLQ, Products_InventoryUpdated_DLQ, Analytics_Events_DLQ
    };

    public static string GetTopicForEventType(string eventType, string? aggregateType = null)
    {
        return (aggregateType, eventType) switch
        {
            ("Order", "Created") => Orders_Created,
            ("Order", "Updated") => Orders_Updated,
            ("Order", "Cancelled") => Orders_Cancelled,
            ("Payment", "Authorized") => Payments_Authorized,
            ("Payment", "Processed") => Payments_Processed,
            ("Payment", "Failed") => Payments_Failed,
            ("Payment", "Refunded") => Payments_Refunded,
            ("Product", "InventoryUpdated") => Products_InventoryUpdated,
            ("Product", "PriceChanged") => Products_PriceChanged,
            ("Cart", "ItemAdded") => Carts_ItemAdded,
            ("Cart", "ItemRemoved") => Carts_ItemRemoved,
            ("Cart", "Cleared") => Carts_Cleared,
            _ => throw new ArgumentException($"Unknown event: {aggregateType}.{eventType}")
        };
    }
}

public static class KafkaConsumerGroups
{
    public const string AnalyticsService = "analytics-service";
    public const string ProductService = "product-service";
    public const string CartService = "cart-service";
    public const string OrderService = "order-service";
    public const string PaymentService = "payment-service";
}