using Confluent.Kafka;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PaymentService.Application.Commands.ProcessPayment;
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

        var failures = 0;
        while (!stoppingToken.IsCancellationRequested)
        {
            ConsumeResult<string, string>? result = null;
            try
            {
                result = consumer.Consume(stoppingToken);
                if (result is null) continue;
                if (result.Message?.Value is null) throw new InvalidDataException("Payment request payload is required.");

                var headers = result.Message.Headers;
                if (headers is null || !headers.TryGetLastBytes("eventId", out var bytes) || bytes.Length != 16)
                    throw new InvalidDataException("Payment request requires a 16-byte eventId header.");

                ProcessAsync(result.Message.Value, new Guid(bytes).ToString(), stoppingToken).GetAwaiter().GetResult();
                consumer.Commit(result);
                failures = 0;
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (ConsumeException ex) when (!ex.Error.IsFatal)
            {
                logger.LogError(ex, "[PaymentService] Kafka consume error");
            }
            catch (Exception ex) when (result is not null)
            {
                failures++;
                var delay = TimeSpan.FromSeconds(Math.Min(30, Math.Pow(2, Math.Min(failures - 1, 5))));
                logger.LogError(ex, "[PaymentService] Failed to process {Offset}; retrying in {Delay}", result.TopicPartitionOffset, delay);
                consumer.Seek(result.TopicPartitionOffset);
                try { Task.Delay(delay, stoppingToken).GetAwaiter().GetResult(); }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            }
        }

        consumer.Close();
    }

    private async Task ProcessAsync(string rawJson, string eventId, CancellationToken ct)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var evt = JsonSerializer.Deserialize<PaymentRequestedIntegrationEvent>(rawJson, Json)
            ?? throw new InvalidDataException("Payment request must not be null.");
        if (evt.OrderId == Guid.Empty || evt.UserId == Guid.Empty || evt.Amount <= 0 || string.IsNullOrWhiteSpace(evt.Currency))
            throw new InvalidDataException("Payment request has invalid order, user, amount or currency.");

        var mediator = scope.ServiceProvider.GetRequiredService<ISender>();
        await mediator.Send(new ProcessPaymentCommand(evt.OrderId, evt.UserId, evt.Amount, evt.Currency, eventId), ct);
    }
}
