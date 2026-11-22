using Confluent.Kafka;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PaymentService.Infrastructure.Persistence;

namespace PaymentService.Infrastructure.Kafka;

/// <summary>Same transactional-outbox poller pattern as OrderService.</summary>
public sealed class OutboxPublisherHostedService(
    IServiceScopeFactory scopeFactory, IProducer<string, string> producer,
    IOptions<PaymentKafkaOptions> options, ILogger<OutboxPublisherHostedService> logger) : BackgroundService
{
    private readonly PaymentKafkaOptions _settings = options.Value;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try { await PublishPendingAsync(stoppingToken); }
            catch (Exception ex) { logger.LogError(ex, "[PaymentService] Outbox publish cycle failed"); }
            await Task.Delay(_settings.OutboxPollIntervalMs, stoppingToken);
        }
    }

    private async Task PublishPendingAsync(CancellationToken ct)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PaymentDbContext>();

        var pending = await db.OutboxMessages.Where(m => m.ProcessedAtUtc == null)
            .OrderBy(m => m.CreatedAtUtc).Take(_settings.OutboxBatchSize).ToListAsync(ct);

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
                logger.LogError(ex, "[PaymentService] Failed to publish outbox message {Id} (attempt {Attempt})", message.Id, message.Attempts);
            }
        }

        await db.SaveChangesAsync(ct);
    }
}
