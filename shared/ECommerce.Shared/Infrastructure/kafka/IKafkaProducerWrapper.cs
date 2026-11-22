namespace ECommerce.Shared.Infrastructure.kafka;

/// <summary>
/// Interface for a Kafka producer wrapper, providing methods to produce messages to Kafka topics with optional headers and cancellation support.
/// </summary>
public interface IKafkaProducer
{
    Task<string> PublishAsync<T>(
        string topic,
        string key,
        T message,
        Dictionary<string, string>? headers = null,
        CancellationToken cancellationToken = default) where T : class;

    Task FlushAsync(TimeSpan timeout);
}