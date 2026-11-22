namespace OrderService.Infrastructure.Kafka;

public sealed class OrderKafkaOptions
{
    public const string Section = "Kafka";
    public string BootstrapServers { get; init; } = "localhost:9092";
    public string ConsumerGroupId { get; init; } = "order-service-group";
    public string ProductStockTopic { get; init; } = "product-stock";
    public string PaymentStatusTopic { get; init; } = "payment-status";
    public int OutboxPollIntervalMs { get; init; } = 1000;
    public int OutboxBatchSize { get; init; } = 50;
}
