namespace ECommerce.Shared.Infrastructure.kafka;

/// <summary>Immutable envelope. Carries partition/offset so callers can commit after success.</summary>
public sealed record KafkaMessage(
    string Topic,
    int Partition,
    long Offset,
    string Key,
    string Value,
    IReadOnlyDictionary<string, string> Headers);

/// <summary>Broker-agnostic consumer abstraction. No Confluent.Kafka types leak through it.</summary>
///interface for consuming messages from Kafka topics, providing methods to subscribe to topics, consume messages, and commit offsets after successful processing.
public interface IKafkaConsumer : IDisposable
{
    void Subscribe(params string[] topics);

    /// <summary>Returns null on cancellation, timeout, or a non-fatal consume error.</summary>
    Task<KafkaMessage?> ConsumeAsync(CancellationToken cancellationToken);

    /// <summary>Commits (offset + 1) for the message's partition. Call only after successful handling.</summary>
    Task CommitAsync(KafkaMessage message, CancellationToken cancellationToken = default);
}