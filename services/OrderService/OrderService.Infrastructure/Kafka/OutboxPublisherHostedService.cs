using Confluent.Kafka;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OrderService.Infrastructure.Persistence;

namespace OrderService.Infrastructure.Kafka;

/// <summary>The ONLY thing that publishes OrderService events to Kafka. Polls
/// OutboxMessages WHERE ProcessedAtUtc IS NULL, publishes each, marks it processed.
/// See ROADMAP for the full transactional-outbox rationale.</summary>
public sealed class OutboxPublisherHostedService(
    IServiceScopeFactory scopeFactory, IProducer<string, string> producer,
    IOptions<OrderKafkaOptions> options, ILogger<OutboxPublisherHostedService> logger) : BackgroundService
{
    private readonly OrderKafkaOptions _settings = options.Value;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try { await PublishPendingAsync(stoppingToken); }
            catch (Exception ex) { logger.LogError(ex, "[OrderService] Outbox publish cycle failed"); }
            await Task.Delay(_settings.OutboxPollIntervalMs, stoppingToken);
        }
    }

    private async Task PublishPendingAsync(CancellationToken ct)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<OrderDbContext>();

        var pending = await db.OutboxMessages
            .Where(m => m.ProcessedAtUtc == null)
            .OrderBy(m => m.CreatedAtUtc)
            .Take(_settings.OutboxBatchSize)
            .ToListAsync(ct);

        foreach (var message in pending)
        {
            try
            {
                await producer.ProduceAsync(message.Topic, new Message<string, string>
                {
                    Key = message.Key, Value = message.Payload,
                    Headers = new Headers { { "eventId", message.Id.ToByteArray() } }
                }, ct);
                message.ProcessedAtUtc = DateTime.UtcNow;
            }
            catch (Exception ex)
            {
                message.Attempts++;
                message.LastError = ex.Message;
                logger.LogError(ex, "[OrderService] Failed to publish outbox message {Id} to {Topic} (attempt {Attempt})", message.Id, message.Topic, message.Attempts);
            }
        }

        await db.SaveChangesAsync(ct);
    }
}
