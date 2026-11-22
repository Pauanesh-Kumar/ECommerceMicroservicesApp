using Confluent.Kafka;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ProductService.Contracts;
using System.Text.Json;

namespace ProductService.Infrastructure.Kafka;

public interface IProductEventPublisher
{
    Task PublishStockReservedAsync(Guid orderId, IReadOnlyList<Guid> productIds, CancellationToken ct = default);
    Task PublishStockReservationFailedAsync(Guid orderId, string reason, IReadOnlyList<Guid> unavailable, CancellationToken ct = default);
}

public sealed class ProductEventPublisher(
    IProducer<string, string> producer,
    IOptions<ProductKafkaOptions> options,
    ILogger<ProductEventPublisher> logger) : IProductEventPublisher, IDisposable
{
    private readonly ProductKafkaOptions _opts = options.Value;
    private static readonly JsonSerializerOptions Json = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    public Task PublishStockReservedAsync(Guid orderId, IReadOnlyList<Guid> productIds, CancellationToken ct = default)
        => PublishAsync(_opts.ProductStockTopic, orderId.ToString(), "StockReserved",
            new StockReservedIntegrationEvent(orderId, productIds, DateTime.UtcNow), ct);

    public Task PublishStockReservationFailedAsync(Guid orderId, string reason, IReadOnlyList<Guid> unavailable, CancellationToken ct = default)
        => PublishAsync(_opts.ProductStockTopic, orderId.ToString(), "StockReservationFailed",
            new StockReservationFailedIntegrationEvent(orderId, reason, unavailable, DateTime.UtcNow), ct);

    private async Task PublishAsync(string topic, string key, string eventType, object payload, CancellationToken ct)
    {
        var envelope = new EventEnvelope(eventType, JsonSerializer.Serialize(payload, Json));
        var msg = new Message<string, string>
        {
            Key = key,
            Value = JsonSerializer.Serialize(envelope, Json),
            Headers = new Headers { { "eventId", Guid.NewGuid().ToByteArray() } }
        };
        try
        {
            var result = await producer.ProduceAsync(topic, msg, ct);
            logger.LogDebug("Published {EventType} to {Topic} partition={P} offset={O}", eventType, topic, result.Partition.Value, result.Offset.Value);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Kafka publish failed topic={Topic} key={Key}", topic, key);
        }
    }

    public void Dispose() => producer.Dispose();
}

public sealed class ProductKafkaOptions
{
    public const string Section = "Kafka";
    public string BootstrapServers { get; init; } = "localhost:9092";
    public string ConsumerGroupId { get; init; } = "product-service-group";
    public string OrderCreatedTopic { get; init; } = "order-created";
    public string StockReleaseTopic { get; init; } = "stock-release";
    public string ProductStockTopic { get; init; } = "product-stock";
}
