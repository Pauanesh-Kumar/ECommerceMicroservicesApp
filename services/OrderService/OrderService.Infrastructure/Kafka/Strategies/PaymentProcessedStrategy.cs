using MediatR;
using OrderService.Application.Commands;
using OrderService.Contracts;
using System.Text.Json;

namespace OrderService.Infrastructure.Kafka.Strategies;

public sealed class PaymentProcessedStrategy(ISender mediator) : IOrderEventStrategy
{
    public string Topic => "payment-status:PaymentProcessed";
    private static readonly JsonSerializerOptions Json = new() { PropertyNameCaseInsensitive = true };

    public async Task HandleAsync(string rawJson, CancellationToken ct)
    {
        var evt = JsonSerializer.Deserialize<PaymentProcessedIntegrationEvent>(rawJson, Json);
        if (evt is null) return;
        await mediator.Send(new ConfirmOrderCommand(evt.OrderId, evt.PaymentId), ct);
    }
}
