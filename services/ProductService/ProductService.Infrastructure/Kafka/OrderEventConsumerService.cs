using Confluent.Kafka;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ProductService.Infrastructure.Persistence;

namespace ProductService.Infrastructure.Kafka;

public sealed class OrderEventConsumerService(
    IServiceScopeFactory scopeFactory,
    IOptions<ProductKafkaOptions> options,
    ILogger<OrderEventConsumerService> logger) : BackgroundService
{
    private readonly ProductKafkaOptions _settings = options.Value;

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

        var failures = 0;
        while (!stoppingToken.IsCancellationRequested)
        {
            ConsumeResult<string, string>? result = null;
            try
            {
                result = consumer.Consume(stoppingToken);
                if (result is null) continue;
                if (result.Message?.Value is null) throw new InvalidDataException("Inventory event payload is required.");
                var headers = result.Message.Headers;
                if (headers is null || !headers.TryGetLastBytes("eventId", out var bytes) || bytes.Length != 16)
                    throw new InvalidDataException("Inventory event requires a 16-byte eventId header.");
                using var scope = scopeFactory.CreateScope();
                var processor = scope.ServiceProvider.GetRequiredService<TransactionalInventoryProcessor>();
                processor.ProcessAsync(result.Topic, result.Message.Value, new Guid(bytes), result.Topic == _settings.OrderCreatedTopic, stoppingToken).GetAwaiter().GetResult();
                consumer.Commit(result);
                failures = 0;
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (ConsumeException ex) when (!ex.Error.IsFatal) { logger.LogError(ex, "[ProductService] Kafka consume error"); }
            catch (Exception ex) when (result is not null)
            {
                failures++;
                var delay = TimeSpan.FromSeconds(Math.Min(30, Math.Pow(2, Math.Min(failures - 1, 5))));
                logger.LogError(ex, "[ProductService] Failed to process {Offset}; retrying in {Delay}", result.TopicPartitionOffset, delay);
                consumer.Seek(result.TopicPartitionOffset);
                try { Task.Delay(delay, stoppingToken).GetAwaiter().GetResult(); }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            }
        }
        consumer.Close();
    }
}
