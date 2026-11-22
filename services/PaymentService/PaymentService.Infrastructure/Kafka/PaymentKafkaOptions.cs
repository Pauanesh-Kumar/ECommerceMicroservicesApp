namespace PaymentService.Infrastructure.Kafka;

public sealed class PaymentKafkaOptions
{
    public const string Section = "Kafka";
    public string BootstrapServers { get; init; } = "localhost:9092";
    public string ConsumerGroupId { get; init; } = "payment-service-group";
    public string PaymentRequestedTopic { get; init; } = "payment-requested";
    public string PaymentStatusTopic { get; init; } = "payment-status";
    public int OutboxPollIntervalMs { get; init; } = 1000;
    public int OutboxBatchSize { get; init; } = 50;
}
