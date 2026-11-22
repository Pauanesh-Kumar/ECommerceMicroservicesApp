using Confluent.Kafka;
using ECommerce.Shared.Constants;
using ECommerce.Shared.Domain;
using Microsoft.Extensions.Logging;
using System.Text;

namespace ECommerce.Shared.Infrastructure.Outbox;

public class OutboxPublisher(IProducer<string, string> producer, ILogger<OutboxPublisher> logger) : IOutboxPublisher
{
    private const int MaxRetries = 3;

    public async Task PublishAsync(IEnumerable<OutboxMessage> messages, CancellationToken cancellationToken = default)
    {
        foreach (var message in messages)
        {
            await PublishSingleAsync(message, cancellationToken);
        }
    }

    public async Task<bool> PublishSingleAsync(OutboxMessage message, CancellationToken cancellationToken = default)
    {
        try
        {
            var topicName = MapAggregateTypeToTopic(message.AggregateType);

            var kafkaMessage = new Message<string, string>
            {
                Key = message.CorrelationId,
                Value = message.Payload,
                Headers = new Headers
                {
                    new("EventType", Encoding.UTF8.GetBytes(message.EventType)),
                    new("CorrelationId", Encoding.UTF8.GetBytes(message.CorrelationId ?? string.Empty)),
                    new("Timestamp", Encoding.UTF8.GetBytes(DateTime.UtcNow.ToString("O")))
                }
            };

            var deliveryReport = await producer.ProduceAsync(topicName, kafkaMessage, cancellationToken);

            logger.LogInformation(
                "Outbox message {OutboxId} published to topic {Topic} (partition {Partition}, offset {Offset})",
                message.Id, topicName, deliveryReport.Partition, deliveryReport.Offset);

            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex,
                "Failed to publish outbox message {OutboxId}. Retry count: {RetryCount}/{MaxRetries}",
                message.Id, message.RetryCount, MaxRetries);

            return false;
        }
    }

    private static string MapAggregateTypeToTopic(string aggregateType) =>
        aggregateType switch
        {
            "Order" => KafkaTopics.OrderEvents,
            "Payment" => KafkaTopics.PaymentEvents,
            "Product" => KafkaTopics.ProductEvents,
            "Cart" => KafkaTopics.CartEvents,
            "Notification" => KafkaTopics.NotificationEvents,
            _ => KafkaTopics.AnalyticsEvents
        };
}