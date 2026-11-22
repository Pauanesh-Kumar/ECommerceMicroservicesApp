namespace NotificationService.Infrastructure.Kafka.Settings;

public sealed class KafkaSettings
{
    public const string SectionName = "Kafka";

    public string BootstrapServers { get; init; } = "localhost:9092";
    public string ConsumerGroupId { get; init; } = "notification-service-group";

    // Topic per originating service — matches docker/kafka-init topic names
    public string UserUpdatedTopic { get; init; } = "user-updated";
    public string OrderPlacedTopic { get; init; } = "order-placed";
    public string PaymentProcessedTopic { get; init; } = "payment-processed";
}