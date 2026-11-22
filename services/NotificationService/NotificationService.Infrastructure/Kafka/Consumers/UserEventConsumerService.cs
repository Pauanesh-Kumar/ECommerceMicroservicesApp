using System.Text.Json;
using Confluent.Kafka;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NotificationService.Infrastructure.Kafka.Contracts;
using NotificationService.Infrastructure.Kafka.Settings;
using NotificationService.Infrastructure.Kafka.Strategies;

namespace NotificationService.Infrastructure.Kafka.Consumers;

/// <summary>
/// Background Kafka consumer for the "user-updated" topic.
/// SRP: only reads messages and delegates to EventStrategyResolver — zero dispatch logic here.
/// </summary>
public sealed class UserEventConsumerService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly KafkaSettings _settings;
    private readonly ILogger<UserEventConsumerService> _logger;

    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };

    public UserEventConsumerService(
        IServiceScopeFactory scopeFactory,
        IOptions<KafkaSettings> options,
        ILogger<UserEventConsumerService> logger)
    {
        _scopeFactory = scopeFactory;
        _settings = options.Value;
        _logger = logger;
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
        => Task.Factory.StartNew(
            () => ConsumeLoop(stoppingToken),
            stoppingToken,
            TaskCreationOptions.LongRunning,
            TaskScheduler.Default);

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
        consumer.Subscribe(_settings.UserUpdatedTopic);
        _logger.LogInformation("[NotificationService] Subscribed to '{Topic}'", _settings.UserUpdatedTopic);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var result = consumer.Consume(stoppingToken);
                if (result?.Message?.Value is null) continue;

                _logger.LogInformation(
                    "[NotificationService] Consumed message at {Offset} from {Topic}: {Value}",
                    result.Offset,
                    result.Topic,
                    result.Message.Value);

                ProcessAsync(result.Message.Value, stoppingToken).GetAwaiter().GetResult();
                consumer.Commit(result);
            }
            catch (OperationCanceledException) { break; }
            catch (ConsumeException ex)
            {
                _logger.LogError(ex, "[NotificationService] Kafka consume error on '{Topic}'", _settings.UserUpdatedTopic);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[NotificationService] Unhandled error processing message");
            }
        }

        consumer.Close();
        _logger.LogInformation("[NotificationService] UserEventConsumerService shut down.");
    }

    private async Task ProcessAsync(string rawJson, CancellationToken ct)
    {
        var envelope = JsonSerializer.Deserialize<UserIntegrationEvent>(rawJson, JsonOpts);
        if (envelope is null)
        {
            _logger.LogWarning("[NotificationService] Failed to deserialize UserIntegrationEvent. Raw={Raw}", rawJson);
            return;
        }

        // Map raw string → typed enum
        var eventType = EventTypeMapper.Resolve(envelope.EventType);

        _logger.LogInformation(
            "[NotificationService] EventType={EventType} (raw={Raw}) UserId={UserId}",
            eventType, envelope.EventType, envelope.UserId);

        if (eventType == Domain.Enums.NotificationEventType.Unknown)
        {
            _logger.LogWarning("[NotificationService] No mapping for raw EventType='{Raw}' — skipped", envelope.EventType);
            return;
        }

        await using var scope = _scopeFactory.CreateAsyncScope();
        var resolver = scope.ServiceProvider.GetRequiredService<EventStrategyResolver>();
        var strategy = resolver.Resolve(eventType);

        if (strategy is null)
        {
            _logger.LogWarning("[NotificationService] No strategy registered for EventType={EventType} — skipped", eventType);
            return;
        }

        await strategy.HandleAsync(envelope, ct);
    }
}