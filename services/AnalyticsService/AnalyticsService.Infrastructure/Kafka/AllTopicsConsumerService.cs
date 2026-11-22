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

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var result = consumer.Consume(stoppingToken);
                if (result?.Message?.Value is null) continue;

                ProcessAsync(result.Topic, result.Message.Value, stoppingToken).GetAwaiter().GetResult();
                consumer.Commit(result);
            }
            catch (OperationCanceledException) { break; }
            catch (ConsumeException ex) { logger.LogError(ex, "[AnalyticsService] Kafka consume error"); }
            catch (Exception ex) { logger.LogError(ex, "[AnalyticsService] Unhandled processing error"); }
        }

        consumer.Close();
    }

    private async Task ProcessAsync(string topic, string rawJson, CancellationToken ct)
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
            if (root.TryGetProperty("eventType", out var et)) eventType = et.GetString() ?? topic;
            if (root.TryGetProperty("orderId", out var oid) && oid.TryGetGuid(out var g)) orderId = g;
            if (root.TryGetProperty("amount", out var amt) && amt.TryGetDecimal(out var a)) amount = a;
        }
        catch (JsonException) { /* best-effort enrichment only — never block ingestion on parse errors */ }

        await repo.InsertAsync(new AnalyticsEvent
        {
            SourceTopic = topic, EventType = eventType, OrderId = orderId, Amount = amount, RawPayload = rawJson
        }, ct);
    }
}
