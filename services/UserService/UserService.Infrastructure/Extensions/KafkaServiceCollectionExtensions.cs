using Confluent.Kafka;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using UserService.Application.Interfaces;
using UserService.Infrastructure.Kafka;

namespace UserService.Infrastructure.Extensions;

public static class KafkaServiceCollectionExtensions
{
    public static IServiceCollection AddKafkaProducer(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<KafkaOptions>(configuration.GetSection(KafkaOptions.Section));

        var bootstrap = configuration["Kafka:BootstrapServers"];
        if (string.IsNullOrWhiteSpace(bootstrap))
            throw new InvalidOperationException(
                "Kafka:BootstrapServers not configured. Set Kafka__BootstrapServers.");

        var cfg = new ProducerConfig
        {
            BootstrapServers = bootstrap,
            Acks = Acks.All,
            EnableIdempotence = true,
            MessageSendMaxRetries = 3,
            RetryBackoffMs = 100,
            CompressionType = CompressionType.Snappy
        };

        services.AddSingleton<IProducer<string, string>>(
            _ => new ProducerBuilder<string, string>(cfg).Build());

        // Publisher is stateless and safe as singleton (depends on singleton producer/options/logger).
        services.AddSingleton<IUserEventPublisher, UserEventPublisher>();

        return services;
    }
}