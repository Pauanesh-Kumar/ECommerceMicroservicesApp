using AnalyticsService.Domain.Entities;
using AnalyticsService.Domain.Repositories;
using Confluent.Kafka;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Text.Json;

namespace AnalyticsService.Infrastructure.Kafka;

/// <summary>One consumer, subscribed to every business topic, own consumer group so it
/// reads independently of (and never interferes with) the actual saga participants.
/// Writes everything to the Mongo "bronze layer" verbatim.</summary>
public sealed class AllTopicsConsumerService(
    IServiceScopeFactory scopeFactory, IOptions<AnalyticsKafkaOptions> options, ILogger<AllTopicsConsumerService> logger) : BackgroundService
{
    private readonly AnalyticsKafkaOptions _settings = options.Value;

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
        consumer.Subscribe(_settings.Topics);
        logger.LogInformation("[AnalyticsService] Subscribed to {Topics}", string.Join(",", _settings.Topics));

        var failures = 0;
        var indexReady = false;
        while (!stoppingToken.IsCancellationRequested)
        {
            ConsumeResult<string, string>? result = null;
            try
            {
                if (!indexReady)
                {
                    using var scope = scopeFactory.CreateScope();
                    scope.ServiceProvider.GetRequiredService<IAnalyticsRepository>()
                        .EnsureSourceEventIndexAsync(stoppingToken).GetAwaiter().GetResult();
                    indexReady = true;
                }

                result = consumer.Consume(stoppingToken);
                if (result?.Message?.Value is null) continue;

                var sourceId = GetSourceEventId(result);
                ProcessAsync(result.Topic, result.Message.Value, sourceId, stoppingToken).GetAwaiter().GetResult();
                consumer.Commit(result);
                failures = 0;
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (ConsumeException ex) when (!ex.Error.IsFatal)
            {
                logger.LogError(ex, "[AnalyticsService] Kafka consume error");
            }
            catch (Exception ex) when (result is not null || !indexReady)
            {
                failures++;
                var delay = TimeSpan.FromSeconds(Math.Min(30, Math.Pow(2, Math.Min(failures - 1, 5))));
                logger.LogError(ex, "[AnalyticsService] Ingestion failed at {Offset}; retrying in {Delay}", result?.TopicPartitionOffset, delay);
                if (result is not null) consumer.Seek(result.TopicPartitionOffset);
                try { Task.Delay(delay, stoppingToken).GetAwaiter().GetResult(); }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            }
        }

        consumer.Close();
    }

    private static string GetSourceEventId(ConsumeResult<string, string> result)
    {
        if (result.Message.Headers is not null && result.Message.Headers.TryGetLastBytes("eventId", out var bytes) && bytes.Length == 16)
            return $"{result.Topic}:event:{new Guid(bytes):N}";
        return $"{result.Topic}:offset:{result.Partition.Value}:{result.Offset.Value}";
    }

    private async Task ProcessAsync(string topic, string rawJson, string sourceId, CancellationToken ct)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var repo = scope.ServiceProvider.GetRequiredService<IAnalyticsRepository>();

        string eventType = topic;
        Guid? orderId = null;
        decimal? amount = null;

        try
        {
            using var doc = JsonDocument.Parse(rawJson);
            var root = doc.RootElement;
            // Envelope topics (product-stock, payment-status) carry {eventType, payload};
            // direct-shape topics (order-created, order-confirmed, ...) carry the DTO directly.
            if (root.ValueKind == JsonValueKind.Object)
            {
                if (root.TryGetProperty("eventType", out var et) && et.ValueKind == JsonValueKind.String) eventType = et.GetString() ?? topic;
                if (root.TryGetProperty("orderId", out var oid) && oid.ValueKind == JsonValueKind.String && oid.TryGetGuid(out var g)) orderId = g;
                if (root.TryGetProperty("amount", out var amt) && amt.ValueKind == JsonValueKind.Number && amt.TryGetDecimal(out var a)) amount = a;
            }
        }
        catch (JsonException) { /* best-effort enrichment only — never block ingestion on parse errors */ }

        await repo.InsertAsync(new AnalyticsEvent
        {
            SourceEventId = sourceId, SourceTopic = topic, EventType = eventType, OrderId = orderId, Amount = amount, RawPayload = rawJson
        }, ct);
    }
}
