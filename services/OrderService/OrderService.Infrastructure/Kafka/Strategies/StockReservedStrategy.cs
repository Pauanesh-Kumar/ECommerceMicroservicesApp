using MediatR;
using OrderService.Application.Commands;
using OrderService.Contracts;
using System.Text.Json;

namespace OrderService.Infrastructure.Kafka.Strategies;

public sealed class StockReservedStrategy(ISender mediator) : IOrderEventStrategy
{
    public string Topic => "product-stock:StockReserved";
    private static readonly JsonSerializerOptions Json = new() { PropertyNameCaseInsensitive = true };

    public async Task HandleAsync(string rawJson, CancellationToken ct)
    {
        var evt = JsonSerializer.Deserialize<StockReservedIntegrationEvent>(rawJson, Json);
        if (evt is null) return;
        await mediator.Send(new MarkAwaitingPaymentCommand(evt.OrderId), ct);
    }
}
