using ApiGateway.Configuration;
using Confluent.Kafka;

namespace ApiGateway.Extensions;

public static class KafkaExtensions
{
    /// <summary>
    /// Registers a singleton Kafka producer for fire-and-forget audit events.
    /// The Gateway does NOT consume Kafka — consumption already lives in
    /// NotificationService.Infrastructure.Kafka.Consumers.UserEventConsumerService.
    /// Topic "user-updated" matches UserService.Infrastructure.Kafka.KafkaOptions.UserUpdatedTopic.
    /// </summary>
    public static IServiceCollection AddGatewayKafka(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var opts = configuration
            .GetSection(KafkaOptions.Section)
            .Get<KafkaOptions>()
            ?? throw new InvalidOperationException("Kafka configuration section is missing.");

        services.Configure<KafkaOptions>(configuration.GetSection(KafkaOptions.Section));

        services.AddSingleton<IProducer<string, string>>(_ =>
            new ProducerBuilder<string, string>(new ProducerConfig
            {
                BootstrapServers = opts.BootstrapServers,
                Acks = Acks.All,
                MessageTimeoutMs = 5000,
                EnableIdempotence = true
            }).Build());

        return services;
    }
}
