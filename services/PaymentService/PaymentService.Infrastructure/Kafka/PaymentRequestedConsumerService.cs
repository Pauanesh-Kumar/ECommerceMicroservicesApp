using Confluent.Kafka;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PaymentService.Application.Commands.ProcessPayment;
using PaymentService.Application.Interfaces;
using PaymentService.Contracts;
using System.Text.Json;

namespace PaymentService.Infrastructure.Kafka;

public sealed class PaymentRequestedConsumerService(
    IServiceScopeFactory scopeFactory, IOptions<PaymentKafkaOptions> options, ILogger<PaymentRequestedConsumerService> logger) : BackgroundService
{
    private readonly PaymentKafkaOptions _settings = options.Value;
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
        consumer.Subscribe(_settings.PaymentRequestedTopic);
        logger.LogInformation("[PaymentService] Subscribed to '{Topic}'", _settings.PaymentRequestedTopic);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var result = consumer.Consume(stoppingToken);
                if (result?.Message?.Value is null) continue;

                var eventId = result.Message.Headers.TryGetLastBytes("eventId", out var b) && b.Length == 16
                    ? new Guid(b).ToString() : Guid.NewGuid().ToString();

                ProcessAsync(result.Message.Value, eventId, stoppingToken).GetAwaiter().GetResult();
                consumer.Commit(result);
            }
            catch (OperationCanceledException) { break; }
            catch (ConsumeException ex) { logger.LogError(ex, "[PaymentService] Kafka consume error"); }
            catch (Exception ex) { logger.LogError(ex, "[PaymentService] Unhandled processing error"); }
        }

        consumer.Close();
    }

    private async Task ProcessAsync(string rawJson, string eventId, CancellationToken ct)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var uow = scope.ServiceProvider.GetRequiredService<IPaymentUnitOfWork>();

        uow.RecordInboxEvent(eventId);
        if (!await uow.TrySaveChangesAsync(ct))
        {
            logger.LogDebug("[PaymentService] Duplicate eventId={EventId} — skipped", eventId);
            return;
        }

        var evt = JsonSerializer.Deserialize<PaymentRequestedIntegrationEvent>(rawJson, Json);
        if (evt is null) return;

        var mediator = scope.ServiceProvider.GetRequiredService<ISender>();
        await mediator.Send(new ProcessPaymentCommand(evt.OrderId, evt.UserId, evt.Amount, evt.Currency), ct);
    }
}
