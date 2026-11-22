using Confluent.Kafka;
using Microsoft.Extensions.Logging;
using System.Text;

namespace ECommerce.Shared.Infrastructure.kafka;

public sealed class KafkaConsumerWrapper : IKafkaConsumer
{
    private readonly IConsumer<string, string> _consumer;
    private readonly ILogger<KafkaConsumerWrapper> _logger;

    public KafkaConsumerWrapper(ConsumerConfig config, ILogger<KafkaConsumerWrapper> logger)
    {
        _logger = logger;

        // Copy so the caller's (possibly shared) config is never mutated
        var cfg = new ConsumerConfig(config)
        {
            EnableAutoCommit = false,               // commit only after successful processing
            AutoOffsetReset = AutoOffsetReset.Earliest,
            SessionTimeoutMs = 30_000
        };

        _consumer = new ConsumerBuilder<string, string>(cfg)
            .SetErrorHandler((_, e) => LogKafkaError(e))   // instance method: fixes CS9105 / CS1501
            .Build();
    }

    public void Subscribe(params string[] topics)
    {
        _logger.LogInformation("Subscribing to topics: {Topics}", string.Join(", ", topics));
        _consumer.Subscribe(topics);
    }

    public Task<KafkaMessage?> ConsumeAsync(CancellationToken cancellationToken)
        // Consume() blocks, so run it off the caller's thread
        => Task.Run<KafkaMessage?>(() =>
        {
            try
            {
                var result = _consumer.Consume(cancellationToken);
                if (result?.Message is null) return null;

                var headers = new Dictionary<string, string>();
                if (result.Message.Headers is not null)
                {
                    foreach (var h in result.Message.Headers)
                        headers[h.Key] = Encoding.UTF8.GetString(h.GetValueBytes());   // fixes CS1061
                }

                return new KafkaMessage(
                    result.Topic,
                    result.Partition.Value,
                    result.Offset.Value,
                    result.Message.Key ?? string.Empty,
                    result.Message.Value,
                    headers);
            }
            catch (OperationCanceledException) { return null; }
            catch (ConsumeException ex)
            {
                _logger.LogError(ex, "Kafka consume error: {Reason}", ex.Error.Reason);
                return null;
            }
        }, cancellationToken);

    public Task CommitAsync(KafkaMessage message, CancellationToken cancellationToken = default)
    {
        // fixes CS1503: commit the NEXT offset to read (offset + 1)
        _consumer.Commit([new TopicPartitionOffset(message.Topic, message.Partition, message.Offset + 1)]);
        return Task.CompletedTask;
    }

    private void LogKafkaError(Error error)
    {
        if (error.IsFatal)
            _logger.LogError("Kafka fatal: {Code} {Reason}", error.Code, error.Reason);
        else
            _logger.LogWarning("Kafka warning: {Code} {Reason}", error.Code, error.Reason);
    }

    public void Dispose()
    {
        try { _consumer.Close(); }      // leave the group cleanly
        catch (Exception ex) { _logger.LogWarning(ex, "Error closing Kafka consumer"); }
        finally { _consumer.Dispose(); }
    }
}