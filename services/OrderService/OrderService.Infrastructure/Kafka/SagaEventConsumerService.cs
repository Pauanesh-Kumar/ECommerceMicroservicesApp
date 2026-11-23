using Confluent.Kafka;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OrderService.Contracts;
using OrderService.Infrastructure.Kafka.Strategies;
using OrderService.Infrastructure.Persistence;
using System.Text.Json;

namespace OrderService.Infrastructure.Kafka;

/// <summary>
/// Consumes product-stock + payment-status (the two topics OrderService reacts to as a
/// saga participant). Unwraps the EventEnvelope, de-dupes via the Inbox table, then
/// dispatches to the matching IOrderEventStrategy.
/// </summary>
public sealed class SagaEventConsumerService(
    IServiceScopeFactory scopeFactory, IOptions<OrderKafkaOptions> options, ILogger<SagaEventConsumerService> logger) : BackgroundService
{
    private readonly OrderKafkaOptions _settings = options.Value;
    private static readonly JsonSerializerOptions Json = new() { PropertyNameCaseInsensitive = true };

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
        => Task.Factory.StartNew(() => ConsumeLoop(stoppingToken), stoppingToken, TaskCreationOptions.LongRunning, TaskScheduler.Default);

    private void ConsumeLoop(CancellationToken stoppingToken)
    {
        var config = new ConsumerConfig
        {
            BootstrapServers = _settings.BootstrapServers,
            GroupId = _settings.ConsumerGroupId,
            AutoOffsetReset = AutoOffsetReset.Earliest,
            EnableAutoCommit = false
        };

        using var consumer = new ConsumerBuilder<string, string>(config).Build();
        consumer.Subscribe([_settings.ProductStockTopic, _settings.PaymentStatusTopic]);
        logger.LogInformation("[OrderService] Subscribed to {Topics}", string.Join(",", _settings.ProductStockTopic, _settings.PaymentStatusTopic));

        var failures = 0;
        while (!stoppingToken.IsCancellationRequested)
        {
            ConsumeResult<string, string>? result = null;
            try
            {
                result = consumer.Consume(stoppingToken);
                if (result?.Message?.Value is null) continue;

                var eventId = ExtractEventId(result.Message.Headers);
                ProcessAsync(result.Topic, result.Message.Value, eventId, stoppingToken).GetAwaiter().GetResult();
                consumer.Commit(result);
                failures = 0;
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (ConsumeException ex) when (!ex.Error.IsFatal)
            {
                logger.LogError(ex, "[OrderService] Kafka consume error");
            }
            catch (Exception ex) when (result is not null)
            {
                failures++;
                var delay = TimeSpan.FromSeconds(Math.Min(30, Math.Pow(2, Math.Min(failures - 1, 5))));
                logger.LogError(ex, "[OrderService] Failed to process {Offset}; retrying in {Delay}", result.TopicPartitionOffset, delay);
                consumer.Seek(result.TopicPartitionOffset);
                try { Task.Delay(delay, stoppingToken).GetAwaiter().GetResult(); }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            }
        }

        consumer.Close();
    }

    private static Guid ExtractEventId(Headers headers)
        => headers is not null && headers.TryGetLastBytes("eventId", out var bytes) && bytes.Length == 16
            ? new Guid(bytes)
            : throw new InvalidDataException("Saga event requires a 16-byte eventId header.");

    private async Task ProcessAsync(string topic, string rawJson, Guid eventId, CancellationToken ct)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<OrderDbContext>();

        if (await db.InboxMessages.AnyAsync(message => message.EventId == eventId, ct))
        {
            logger.LogDebug("[OrderService] Duplicate eventId={EventId} — skipped", eventId);
            return;
        }

        var envelope = JsonSerializer.Deserialize<EventEnvelope>(rawJson, Json)
            ?? throw new InvalidDataException("Saga event envelope must not be null.");
        var resolver = scope.ServiceProvider.GetRequiredService<OrderEventStrategyResolver>();
        var strategy = resolver.Resolve(topic, envelope.EventType);
        if (strategy is null)
        {
            logger.LogWarning("[OrderService] No strategy for {Topic}:{EventType} — skipped", topic, envelope.EventType);
            return;
        }

        // The strategy and unit of work share this scoped context: its SaveChanges
        // commits the inbox record together with the order transition and outbox.
        db.InboxMessages.Add(new InboxMessage { EventId = eventId });
        await strategy.HandleAsync(envelope.Payload, ct);
        await db.SaveChangesAsync(ct);
    }
}
