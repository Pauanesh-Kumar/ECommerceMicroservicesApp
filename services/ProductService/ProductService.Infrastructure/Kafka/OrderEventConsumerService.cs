using Confluent.Kafka;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ProductService.Application.Commands.ReleaseStock;
using ProductService.Application.Commands.ReserveStock;
using ProductService.Contracts;
using ProductService.Infrastructure.Persistence;
using System.Text.Json;

namespace ProductService.Infrastructure.Kafka;

/// <summary>Subscribes to "order-created" (reserve) and "stock-release" (compensate) —
/// same multiplexed-BackgroundService shape as NotificationService's per-topic
/// consumers. Every message is Inbox-deduplicated before any side effect runs.</summary>
public sealed class OrderEventConsumerService(
    IServiceScopeFactory scopeFactory,
    IOptions<ProductKafkaOptions> options,
    ILogger<OrderEventConsumerService> logger) : BackgroundService
{
    private readonly ProductKafkaOptions _settings = options.Value;
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
        => Task.Factory.StartNew(() => ConsumeLoop(stoppingToken), stoppingToken,
            TaskCreationOptions.LongRunning, TaskScheduler.Default);

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
        consumer.Subscribe([_settings.OrderCreatedTopic, _settings.StockReleaseTopic]);
        logger.LogInformation("[ProductService] Subscribed to {Topics}", string.Join(",", _settings.OrderCreatedTopic, _settings.StockReleaseTopic));

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var result = consumer.Consume(stoppingToken);
                if (result?.Message?.Value is null) continue;

                var eventId = ExtractEventId(result.Message.Headers);
                ProcessAsync(result.Topic, result.Message.Value, eventId, stoppingToken).GetAwaiter().GetResult();
                consumer.Commit(result);
            }
            catch (OperationCanceledException) { break; }
            catch (ConsumeException ex) { logger.LogError(ex, "[ProductService] Kafka consume error"); }
            catch (Exception ex) { logger.LogError(ex, "[ProductService] Unhandled processing error"); }
        }

        consumer.Close();
    }

    private static Guid ExtractEventId(Headers headers)
        => headers.TryGetLastBytes("eventId", out var bytes) && bytes.Length == 16 ? new Guid(bytes) : Guid.NewGuid();

    private async Task ProcessAsync(string topic, string rawJson, Guid eventId, CancellationToken ct)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var inbox = scope.ServiceProvider.GetRequiredService<InboxStore>();

        if (!await inbox.TryMarkProcessedAsync(eventId, ct))
        {
            logger.LogDebug("[ProductService] Duplicate eventId={EventId} — skipped", eventId);
            return;
        }

        var mediator = scope.ServiceProvider.GetRequiredService<ISender>();
        var publisher = scope.ServiceProvider.GetRequiredService<IProductEventPublisher>();

        if (topic == _settings.OrderCreatedTopic)
        {
            var evt = JsonSerializer.Deserialize<OrderCreatedIntegrationEvent>(rawJson, JsonOpts);
            if (evt is null) return;

            var result = await mediator.Send(new ReserveStockCommand(evt.OrderId, evt.Items), ct);
            if (result.Success)
                await publisher.PublishStockReservedAsync(evt.OrderId, result.ReservedProductIds, ct);
            else
                await publisher.PublishStockReservationFailedAsync(evt.OrderId, "Insufficient stock", result.UnavailableProductIds, ct);
        }
        else if (topic == _settings.StockReleaseTopic)
        {
            var evt = JsonSerializer.Deserialize<StockReleaseRequestedEvent>(rawJson, JsonOpts);
            if (evt is null) return;
            await mediator.Send(new ReleaseStockCommand(evt.OrderId, evt.Items), ct);
        }
    }
}
