using Confluent.Kafka;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace ECommerce.Shared.Infrastructure.kafka;

/// <summary>
/// Wrapper for Kafka producer with standardized error handling and logging.
/// </summary>

public class KafkaProducerWrapper : IKafkaProducer, IAsyncDisposable
{
    private readonly IProducer<string, string> _producer;
    private readonly ILogger<KafkaProducerWrapper> _logger;

    public KafkaProducerWrapper(ProducerConfig config, ILogger<KafkaProducerWrapper> logger)
    {
        _logger = logger;
        config.EnableIdempotence = true;           // was EnableIdempotentWrites
        config.Acks = Acks.All;
        config.MessageSendMaxRetries = 3;          // was Retries
        config.EnableDeliveryReports = true;

        _producer = new ProducerBuilder<string, string>(config)
            .SetErrorHandler((_, e) => LogError(e))
            .SetStatisticsHandler((_, json) => LogStatistics(json))
            .Build();

        _logger.LogInformation("Kafka producer initialized");
    }

    public async Task<string> PublishAsync<T>(
        string topic,
        string key,
        T message,
        Dictionary<string, string>? headers = null,
        CancellationToken cancellationToken = default) where T : class
    {
        try
        {
            var jsonPayload = JsonSerializer.Serialize(message);
            var kafkaHeaders = new Headers();

            if (headers != null)
            {
                foreach (var (headerKey, headerValue) in headers)
                {
                    kafkaHeaders.Add(headerKey, System.Text.Encoding.UTF8.GetBytes(headerValue));
                }
            }

            var kmsg = new Message<string, string>
            {
                Key = key,
                Value = jsonPayload,
                Headers = kafkaHeaders
            };

            var deliveryReport = await _producer.ProduceAsync(topic, kmsg, cancellationToken);
            _logger.LogInformation(
                "Published to '{Topic}' Partition={Partition} Offset={Offset}",
                topic, deliveryReport.Partition.Value, deliveryReport.Offset.Value);

            return deliveryReport.Offset.ToString();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to publish to '{Topic}'", topic);
            throw;
        }
    }

    public async Task FlushAsync(TimeSpan timeout)
    {
        _producer.Flush(timeout);
        await Task.CompletedTask;
    }

    private void LogError(Error error)
    {
        if (error.IsFatal)
            _logger.LogError("Kafka FATAL: {Reason}", error.Reason);
        else
            _logger.LogWarning("Kafka warning: {Reason}", error.Reason);
    }

    private void LogStatistics(string json)
    {
        _logger.LogDebug("Kafka stats: {Stats}", json);
    }

    async ValueTask IAsyncDisposable.DisposeAsync()
    {
        _producer?.Dispose();
        await Task.CompletedTask;
    }
}