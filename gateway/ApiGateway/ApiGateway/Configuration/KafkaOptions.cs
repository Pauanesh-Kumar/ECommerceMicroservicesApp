namespace ApiGateway.Configuration;

/// <summary>
/// Gateway-local Kafka producer options.
/// UserUpdatedTopic MUST match UserService KafkaOptions.UserUpdatedTopic ("user-updated")
/// so that the Gateway audit event lands on the same topic the NotificationService already consumes.
/// </summary>
public sealed class KafkaOptions
{
    public const string Section = "Kafka";
    public string BootstrapServers { get; init; } = "localhost:9092";

    // Matches UserService.Infrastructure.Kafka.KafkaOptions.UserUpdatedTopic
    public string UserUpdatedTopic { get; init; } = "user-updated";
}