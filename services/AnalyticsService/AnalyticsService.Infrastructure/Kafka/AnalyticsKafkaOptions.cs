namespace AnalyticsService.Infrastructure.Kafka;

public sealed class AnalyticsKafkaOptions
{
    public const string Section = "Kafka";
    public string BootstrapServers { get; init; } = "localhost:9092";
    // Own consumer group — Analytics must NEVER share a group with a business consumer,
    // or it would steal partitions/offsets from Order/Product/Payment's actual processing.
    public string ConsumerGroupId { get; init; } = "analytics-service-group";
    public string[] Topics { get; init; } =
    [
        "user-updated", "order-created", "product-stock", "payment-requested",
        "payment-status", "order-confirmed", "order-cancelled", "stock-release"
    ];
}
